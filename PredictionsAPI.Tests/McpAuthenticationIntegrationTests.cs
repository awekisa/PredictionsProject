using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PredictionsAPI.Controllers;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Auth;
using PredictionsAPI.Entities;
using PredictionsAPI.Extensions;
using PredictionsAPI.Security;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class McpAuthenticationIntegrationTests
{
    [Fact]
    public async Task RealJwtLoginManagesTokens_AndMcpCannotAuthenticateOrdinaryRoutesOrMintTokens()
    {
        var logs = new CapturedLogs();
        using var server = await CreateServerAsync(logs);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, "alice"));
        var created = await client.PostAsJsonAsync("/api/auth/me/agent-connections", new { name = "Codex" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        created.Headers.CacheControl!.NoStore.Should().BeTrue();
        var token = (await created.Content.ReadFromJsonAsync<CreatedMcpAccessTokenResponse>())!;
        (await client.GetAsync("/rest")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        (await client.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/mcp/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/rest")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/auth/me/agent-connections")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/me/agent-connections", new { name = "Escalation" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync($"/api/auth/me/agent-connections/{token.Connection.Id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/outside-mcp")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        string.Join("\n", logs.Messages).Should().NotContain(token.Token);
    }

    [Fact]
    public async Task OwnershipValidationAndRevocationAreEnforcedOverHttp()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();
        var aliceJwt = await LoginAsync(client, "alice");
        client.DefaultRequestHeaders.Authorization = new("Bearer", aliceJwt);
        foreach (var payload in new object[] {
            new { name = "", expirationDays = 30 }, new { name = "x", expirationDays = 365 },
            new { name = "x", scopes = new[] { "root" } }, new { name = "x", scopes = Array.Empty<string>() }
        })
            (await client.PostAsJsonAsync("/api/auth/me/agent-connections", payload)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/auth/me/agent-connections", new { name = "x", scopes = new[] { "admin:write" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var created = await client.PostAsJsonAsync("/api/auth/me/agent-connections", new { name = "Codex" });
        var token = (await created.Content.ReadFromJsonAsync<CreatedMcpAccessTokenResponse>())!;
        var listing = await client.GetStringAsync("/api/auth/me/agent-connections");
        listing.Should().Contain("Codex").And.NotContain(token.Token).And.NotContain("tokenHash");

        client.DefaultRequestHeaders.Authorization = new("Bearer", await LoginAsync(client, "bob"));
        (await client.GetStringAsync("/api/auth/me/agent-connections")).Should().NotContain("Codex");
        (await client.DeleteAsync($"/api/auth/me/agent-connections/{token.Connection.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        client.DefaultRequestHeaders.Authorization = new("Bearer", aliceJwt);
        (await client.DeleteAsync($"/api/auth/me/agent-connections/{token.Connection.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        client.DefaultRequestHeaders.Add("Mcp-Session-Id", "retained-test-session");
        var denied = await client.GetAsync("/mcp/read");
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        denied.Headers.WwwAuthenticate.Single().Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task RemovingAdminRoleInvalidatesAdminAccessDespiteOldBrowserRoleClaim()
    {
        using var server = await CreateServerAsync();
        using var client = server.CreateClient();
        var jwt = await LoginAsync(client, "admin");
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);
        var created = await client.PostAsJsonAsync("/api/auth/me/agent-connections", new { name = "Admin", scopes = new[] { "admin:read" } });
        var token = (await created.Content.ReadFromJsonAsync<CreatedMcpAccessTokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        (await client.GetAsync("/mcp/admin")).StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = server.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync("admin@test.com"))!;
            (await users.RemoveFromRoleAsync(user, "Admin")).Succeeded.Should().BeTrue();
        }
        (await client.GetAsync("/mcp/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt);
        (await client.PostAsJsonAsync("/api/auth/me/agent-connections", new { name = "Stale", scopes = new[] { "admin:read" } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<string> LoginAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = $"{name}@test.com", password = "TestPass123!" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
    }

    private static async Task<TestServer> CreateServerAsync(CapturedLogs? logs = null)
    {
        var name = Guid.NewGuid().ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "Integration-only-signing-key-not-a-production-secret-12345678",
            ["JwtSettings:Issuer"] = "Tests", ["JwtSettings:Audience"] = "Tests",
            ["JwtSettings:ExpirationInMinutes"] = "60",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
            ["FootballApi:BaseUrl"] = "https://example.invalid/"
        }).Build();
        var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            if (logs is not null) services.AddLogging(builder => builder.AddProvider(logs));
            services.AddSingleton<IConfiguration>(config);
            services.AddPredictionsServices(config);
            services.RemoveAll<AppDbContext>();
            services.AddScoped(_ => DbContextFactory.Create(name));
            services.RemoveAll<IHostedService>();
            services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        }).Configure(app =>
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGet("/rest", () => "ok").RequireAuthorization();
                endpoints.MapGet("/mcp/read", () => "ok").RequireAuthorization(McpScopes.Policy(McpScopes.AppRead));
                endpoints.MapGet("/mcp/admin", () => "ok").RequireAuthorization(McpScopes.Policy(McpScopes.AdminRead));
                endpoints.MapGet("/outside-mcp", () => "ok").RequireAuthorization(McpScopes.Policy(McpScopes.AppRead));
            });
        }));
        using var scope = server.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Admin"));
        foreach (var username in new[] { "alice", "bob", "admin" })
        {
            var user = DbContextFactory.MakeUser(username, username);
            (await users.CreateAsync(user, "TestPass123!")).Succeeded.Should().BeTrue();
            if (username == "admin") await users.AddToRoleAsync(user, "Admin");
        }
        return server;
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);
        public void Dispose() { }

        private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
}
