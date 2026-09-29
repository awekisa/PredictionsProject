using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.OAuth;
using PredictionsAPI.Security;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace PredictionsAPI.Tests;

public class McpOAuthTests
{
    private const string Resource = "https://predictionsproject.onrender.com/mcp";
    private const string Callback = "https://claude.ai/api/mcp/auth_callback";
    private const string Verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
    private static string Challenge => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(Verifier)));

    [Fact]
    public async Task DiscoveryUsesCanonicalUrlsEvenBehindProxy_AndRejectsUnknownRedirects()
    {
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync();
        using var http = server.CreateClient();
        http.DefaultRequestHeaders.Host = "untrusted.example";
        var denied = await http.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "initialize" });
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        denied.Headers.WwwAuthenticate.Single().ToString().Should().Be("Bearer resource_metadata=\"https://predictionsproject.onrender.com/.well-known/oauth-protected-resource\"");
        foreach (var path in new[] { "/.well-known/oauth-protected-resource", "/.well-known/oauth-protected-resource/mcp" })
        {
            var response = await http.GetAsync(path);
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            var doc = await Json(response);
            doc.GetProperty("resource").GetString().Should().Be(Resource);
        }
        var metadata = await Json(await http.GetAsync("/.well-known/oauth-authorization-server"));
        metadata.GetProperty("code_challenge_methods_supported")[0].GetString().Should().Be("S256");
        metadata.GetProperty("client_id_metadata_document_supported").GetBoolean().Should().BeTrue();
        foreach (var callback in new[] { Callback + "/", "https://claude.ai.evil.example/api/mcp/auth_callback", "https://evil.example/", "https://claude.ai/*" })
            (await http.PostAsJsonAsync("/oauth/register", new { client_name = "Claude", redirect_uris = new[] { callback }, token_endpoint_auth_method = "none" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var client = await Register(http);
        (await http.GetAsync(AuthorizeUrl(client, callback: "https://evil.example/"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.GetAsync(AuthorizeUrl(client, method: "plain"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.GetAsync(AuthorizeUrl(client, resource: "https://other.example/mcp"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.GetAsync(AuthorizeUrl(client) + "&resource=" + Uri.EscapeDataString(Resource))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CodePkceRefreshScopesAndRevocation_WorkThroughRealHttpAndMcp()
    {
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync();
        using var http = server.CreateClient();
        var client = await Register(http);
        var code = await Approve(http, client);
        (await Exchange(http, client, code, verifier: new string('x', 43))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Exchange(http, client, code, resource: "https://other.example/mcp")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Exchange(http, client, code, callback: Callback + "/")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var token = await Json(await Exchange(http, client, code));
        var access = token.GetProperty("access_token").GetString()!;
        var refresh = token.GetProperty("refresh_token").GetString()!;
        (await Exchange(http, client, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        http.DefaultRequestHeaders.Authorization = new("Bearer", access);
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.GetAsync("/mcp/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await http.GetAsync("/rest")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await http.GetAsync("/api/auth/me/agent-connections")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await using (var mcp = await McpClient.CreateAsync(new HttpClientTransport(new()
        { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp }, http)))
        {
            (await mcp.ListToolsAsync()).Should().NotBeEmpty();
            var current = await mcp.CallToolAsync("get_current_user");
            current.IsError.Should().NotBeTrue();
            var text = string.Join(" ", current.Content.OfType<TextContentBlock>().Select(x => x.Text));
            text.Should().Contain("alice").And.Contain("app:read");
            foreach (var tool in new[] { "list_tournaments", "get_global_standings" })
                (await mcp.CallToolAsync(tool)).IsError.Should().NotBeTrue();
            (await mcp.CallToolAsync("save_my_prediction", new Dictionary<string, object?>())).IsError.Should().BeTrue();
        }
        http.DefaultRequestHeaders.Authorization = null;
        var rotated = await Json(await Refresh(http, client, refresh));
        http.DefaultRequestHeaders.Authorization = new("Bearer", access);
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        http.DefaultRequestHeaders.Authorization = new("Bearer", rotated.GetProperty("access_token").GetString());
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.OK);
        http.DefaultRequestHeaders.Authorization = new("Bearer", await McpAuthenticationIntegrationTests.LoginAsync(http, "alice"));
        var list = await Json(await http.GetAsync("/api/auth/me/agent-connections"));
        var connection = list.GetProperty("tokens")[0];
        connection.GetProperty("isOAuth").GetBoolean().Should().BeTrue();
        connection.GetProperty("name").GetString().Should().Be("Claude");
        connection.GetProperty("lastUsedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        (await http.DeleteAsync("/api/auth/me/agent-connections/" + connection.GetProperty("id").GetString())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        http.DefaultRequestHeaders.Authorization = new("Bearer", rotated.GetProperty("access_token").GetString());
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Refresh(http, client, rotated.GetProperty("refresh_token").GetString()!)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminRoleIsRechecked_RefreshReplayRevokesFamily_DeletedOwnerCannotRefresh()
    {
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync();
        using var http = server.CreateClient();
        var client = await Register(http);
        var code = await Approve(http, client, "admin", ["app:read", "admin:read"]);
        var token = await Json(await Exchange(http, client, code));
        http.DefaultRequestHeaders.Authorization = new("Bearer", token.GetProperty("access_token").GetString());
        (await http.GetAsync("/mcp/admin")).StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserRoles.RemoveRange(await db.UserRoles.Where(x => x.UserId == "admin").ToListAsync());
            await db.SaveChangesAsync();
        }
        (await http.GetAsync("/mcp/admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var rotated = await Json(await Refresh(http, client, token.GetProperty("refresh_token").GetString()!));
        (await Refresh(http, client, token.GetProperty("refresh_token").GetString()!)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        http.DefaultRequestHeaders.Authorization = new("Bearer", rotated.GetProperty("access_token").GetString());
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        code = await Approve(http, client);
        token = await Json(await Exchange(http, client, code));
        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Remove((await db.Users.FindAsync("alice"))!);
            await db.SaveChangesAsync();
        }
        http.DefaultRequestHeaders.Authorization = new("Bearer", token.GetProperty("access_token").GetString());
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Refresh(http, client, token.GetProperty("refresh_token").GetString()!)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConsentRequiresBrowserJwt_HidesAdmin_RejectsScopeEscalation_AndSupportsDenial()
    {
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync();
        using var http = server.CreateClient();
        var client = await Register(http);
        var start = await http.GetAsync(AuthorizeUrl(client, scopes: "app:read admin:read"));
        var id = QueryHelpers.ParseQuery(start.Headers.Location!.Query)["request"].ToString();
        var path = "/api/auth/mcp-oauth/consent/" + id;
        (await http.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        http.DefaultRequestHeaders.Authorization = new("Bearer", await McpAuthenticationIntegrationTests.LoginAsync(http, "alice"));
        var consent = await Json(await http.GetAsync(path));
        consent.GetProperty("scopes").EnumerateArray().Select(x => x.GetString()).Should().Equal("app:read");
        (await http.PostAsJsonAsync(path, new { approve = true, scopes = new[] { "admin:read" } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.PostAsJsonAsync(path, new { approve = true, scopes = new[] { "predictions:write" } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var denied = await Json(await http.PostAsJsonAsync(path, new { approve = false }));
        var uri = new Uri(denied.GetProperty("redirectUrl").GetString()!);
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["error"].ToString().Should().Be("access_denied");
        query["state"].ToString().Should().Be("test-state");
        query["iss"].ToString().Should().Be("https://predictionsproject.onrender.com");
        (await http.PostAsJsonAsync(path, new { approve = true, scopes = new[] { "app:read" } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ClaudeOriginIsAccepted_UntrustedOriginsAndDisabledOAuthAreBlocked()
    {
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync();
        using var http = server.CreateClient();
        http.DefaultRequestHeaders.Add("Origin", "https://claude.ai");
        (await http.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "initialize" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        http.DefaultRequestHeaders.Remove("Origin");
        http.DefaultRequestHeaders.Add("Origin", "https://claude.ai.evil.example");
        (await http.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "initialize" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        server.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Mcp:Enabled"] = "false";
        foreach (var path in new[] { "/.well-known/oauth-protected-resource", "/.well-known/oauth-protected-resource/mcp", "/.well-known/oauth-authorization-server", "/oauth/authorize", "/api/auth/mcp-oauth/consent/id" })
            (await http.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await http.PostAsJsonAsync("/oauth/register", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("client_secret_post")]
    [InlineData("client_secret_basic")]
    public async Task DynamicRegistrationAuthenticatesEachSupportedClientType(string method)
    {
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync();
        using var http = server.CreateClient();
        var registration = await Json(await http.PostAsJsonAsync("/oauth/register", new
        { client_name = "Claude", redirect_uris = new[] { Callback }, token_endpoint_auth_method = method }));
        var id = registration.GetProperty("client_id").GetString()!;
        var code = await Approve(http, id);
        if (method == "none")
        {
            registration.TryGetProperty("client_secret", out _).Should().BeFalse();
            (await Exchange(http, id, code)).StatusCode.Should().Be(HttpStatusCode.OK);
            return;
        }
        (await Exchange(http, id, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var fields = new Dictionary<string, string> { ["client_id"] = id, ["code"] = code, ["grant_type"] = "authorization_code",
            ["resource"] = Resource, ["redirect_uri"] = Callback, ["code_verifier"] = Verifier };
        var secret = registration.GetProperty("client_secret").GetString()!;
        if (method == "client_secret_post") fields["client_secret"] = secret;
        else http.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(id + ":" + secret)));
        (await http.PostAsync("/oauth/token", new FormUrlEncodedContent(fields))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MetadataDocumentClientsCanAuthorize_WithAdditionalAdvertisedGrantTypes()
    {
        const string id = "https://claude.ai/oauth/mcp-oauth-client-metadata";
        var resolver = new Moq.Mock<IClientMetadataResolver>();
        resolver.Setup(x => x.ResolveAsync(id, Moq.It.IsAny<CancellationToken>())).ReturnsAsync(new ClientMetadata
        { ClientId = id, ClientName = "Claude", RedirectUris = [Callback], AuthMethod = "none",
            GrantTypes = ["authorization_code", "refresh_token", "urn:ietf:params:oauth:grant-type:jwt-bearer"], ResponseTypes = ["code"] });
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync(configure: services => services.AddSingleton(resolver.Object));
        using var http = server.CreateClient();
        var code = await Approve(http, id);
        (await Exchange(http, id, code)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await http.GetAsync(AuthorizeUrl(id, callback: "https://evil.example/"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreach (var uri in new[] { "http://example.com/client", "https://example.com/", "https://example.com:444/client", "https://user@example.com/client", "https://example.com/client#fragment" })
            await Assert.ThrowsAsync<OAuthException>(() => new ClientMetadataResolver().ResolveAsync(uri, default));
    }

    [Fact]
    public async Task ExpiredCodeAndAccessTokenFail_RefreshKeepsItsOwnLifetime_ResourceBindingIsChecked()
    {
        var clock = new TestClock();
        using var server = await McpAuthenticationIntegrationTests.CreateServerAsync(configure: services => services.AddSingleton<TimeProvider>(clock));
        using var http = server.CreateClient();
        var id = await Register(http);
        var code = await Approve(http, id);
        clock.Now = clock.Now.AddMinutes(3);
        (await Exchange(http, id, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        code = await Approve(http, id);
        var token = await Json(await Exchange(http, id, code));
        clock.Now = clock.Now.AddHours(2);
        http.DefaultRequestHeaders.Authorization = new("Bearer", token.GetProperty("access_token").GetString());
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        token = await Json(await Refresh(http, id, token.GetProperty("refresh_token").GetString()!));
        http.DefaultRequestHeaders.Authorization = new("Bearer", token.GetProperty("access_token").GetString());
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.McpAccessTokens.SingleAsync()).Resource = "https://other.example/mcp";
            await db.SaveChangesAsync();
        }
        (await http.GetAsync("/mcp/read")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Refresh(http, id, token.GetProperty("refresh_token").GetString()!)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData("127.0.0.1")][InlineData("10.1.2.3")][InlineData("169.254.169.254")][InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")][InlineData("fc00::1")][InlineData("fe80::1")][InlineData("100.64.0.1")]
    public void MetadataResolverRejectsPrivateAddresses(string address) => ClientMetadataResolver.IsPublicAddress(IPAddress.Parse(address)).Should().BeFalse();

    internal static async Task<string> Register(HttpClient http)
    {
        var r = await Json(await http.PostAsJsonAsync("/oauth/register", new
        { client_name = "Claude", redirect_uris = new[] { Callback }, token_endpoint_auth_method = "none" }));
        return r.GetProperty("client_id").GetString()!;
    }
    private static string AuthorizeUrl(string client, string callback = Callback, string method = "S256", string resource = Resource, string scopes = "app:read") =>
        QueryHelpers.AddQueryString("/oauth/authorize", new Dictionary<string, string?> { ["client_id"] = client,
            ["redirect_uri"] = callback, ["resource"] = resource, ["response_type"] = "code", ["scope"] = scopes,
            ["code_challenge"] = Challenge, ["code_challenge_method"] = method, ["state"] = "test-state" });
    internal static async Task<string> Approve(HttpClient http, string client, string user = "alice", string[]? scopes = null)
    {
        scopes ??= ["app:read"];
        var start = await http.GetAsync(AuthorizeUrl(client, scopes: string.Join(' ', scopes)));
        start.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var id = QueryHelpers.ParseQuery(start.Headers.Location!.Query)["request"].ToString();
        http.DefaultRequestHeaders.Authorization = new("Bearer", await McpAuthenticationIntegrationTests.LoginAsync(http, user));
        var consent = await Json(await http.PostAsJsonAsync("/api/auth/mcp-oauth/consent/" + id, new { approve = true, scopes }));
        http.DefaultRequestHeaders.Authorization = null;
        return QueryHelpers.ParseQuery(new Uri(consent.GetProperty("redirectUrl").GetString()!).Query)["code"].ToString();
    }
    internal static Task<HttpResponseMessage> Exchange(HttpClient http, string client, string code, string verifier = Verifier, string resource = Resource, string callback = Callback) =>
        http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["code"] = code,
            ["client_id"] = client, ["resource"] = resource, ["redirect_uri"] = callback, ["code_verifier"] = verifier }));
    internal static Task<HttpResponseMessage> Refresh(HttpClient http, string client, string refresh)
    {
        http.DefaultRequestHeaders.Authorization = null;
        return http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh,
            ["client_id"] = client, ["resource"] = Resource }));
    }
    internal static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
