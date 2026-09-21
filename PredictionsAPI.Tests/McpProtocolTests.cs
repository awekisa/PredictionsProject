using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Moq;
using PredictionsAPI.Controllers;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Auth;
using PredictionsAPI.DTOs.Football;
using PredictionsAPI.Entities;
using PredictionsAPI.Extensions;
using PredictionsAPI.Mcp;
using PredictionsAPI.Security;
using PredictionsAPI.Services.Interfaces;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public partial class McpProtocolTests
{
    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task SdkDiscoveryAndTypedTools_WorkForBothProtocolGenerations(string version)
    {
        using var host = await Host.Create();
        await using var client = await host.Connect("alice", [McpScopes.AppRead, McpScopes.PredictionsWrite], version);
        client.NegotiatedProtocolVersion.Should().Be(version);
        var tools = await client.ListToolsAsync();
        tools.Select(t => t.Name).Should().BeEquivalentTo(new[] { "get_current_user", "list_tournaments", "get_tournament", "list_games", "get_game", "get_my_predictions", "get_game_predictions", "get_tournament_standings", "get_global_standings", "get_user_prediction_details", "get_football_standings", "save_my_prediction" }.Concat(AdminToolNames));
        foreach (var tool in tools)
        {
            tool.Description.Should().NotBeNullOrEmpty();
            tool.ProtocolTool.InputSchema.GetProperty("type").GetString().Should().Be("object");
            tool.ProtocolTool.OutputSchema.Should().NotBeNull();
            tool.ProtocolTool.Annotations!.ReadOnlyHint.Should().Be(tool.Name != "save_my_prediction" && (!tool.Name.StartsWith("admin_") || AdminReadNames.Contains(tool.Name)));
        }
        var save = tools.Single(t => t.Name == "save_my_prediction");
        save.ProtocolTool.Annotations!.IdempotentHint.Should().BeTrue();
        save.ProtocolTool.InputSchema.GetProperty("properties").TryGetProperty("userId", out _).Should().BeFalse();
        var user = await Success(client, "get_current_user");
        user.GetProperty("accountId").GetString().Should().Be("alice");
        user.GetProperty("displayName").GetString().Should().Be("alice");
        user.GetProperty("roles").EnumerateArray().Should().BeEmpty();
        user.GetProperty("scopes").EnumerateArray().Should().HaveCount(2);
        (await Success(client, "get_tournament", new { tournamentId = 1 })).GetProperty("id").GetInt32().Should().Be(1);
        (await Success(client, "get_game", new { tournamentId = 1, gameId = 1 })).GetProperty("id").GetInt32().Should().Be(1);
        foreach (var (name, args) in new (string, object)[] {
            ("list_tournaments", new { }), ("list_games", new { tournamentId = 1 }),
            ("get_my_predictions", new { tournamentId = 1 }), ("get_game_predictions", new { gameId = 1 }),
            ("get_tournament_standings", new { tournamentId = 1 }), ("get_global_standings", new { }),
            ("get_user_prediction_details", new { userDisplayName = "alice", tournamentId = (int?)null }),
            ("get_football_standings", new { tournamentId = 1 }) })
            (await Success(client, name, args)).GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task InterleavedAccounts_UpsertOwnRecord_AndRestAgrees()
    {
        using var host = await Host.Create();
        await using var alice = await host.Connect("alice", [McpScopes.AppRead, McpScopes.PredictionsWrite]);
        await using var bob = await host.Connect("bob", [McpScopes.AppRead, McpScopes.PredictionsWrite]);
        var first = await Success(alice, "save_my_prediction", new { gameId = 1, homeGoals = 2, awayGoals = 1 });
        var second = await Success(bob, "save_my_prediction", new { gameId = 1, homeGoals = 0, awayGoals = 0 });
        var again = await Success(alice, "save_my_prediction", new { gameId = 1, homeGoals = 2, awayGoals = 1 });
        again.GetRawText().Should().Be(first.GetRawText());
        first.GetProperty("id").GetInt32().Should().NotBe(second.GetProperty("id").GetInt32());
        foreach (var (client, name) in new[] { (alice, "alice"), (bob, "bob"), (alice, "alice") })
        {
            (await Success(client, "get_current_user")).GetProperty("accountId").GetString().Should().Be(name);
            var items = (await Success(client, "get_my_predictions", new { tournamentId = 1 })).GetProperty("items").EnumerateArray().ToArray();
            items.Should().ContainSingle();
            items[0].GetProperty("userDisplayName").GetString().Should().Be(name);
        }
        using var rest = await host.Rest("alice");
        var restResult = await rest.PostAsJsonAsync("/api/games/1/predictions", new { homeGoals = 3, awayGoals = 1 });
        restResult.EnsureSuccessStatusCode();
        var persisted = (await restResult.Content.ReadFromJsonAsync<JsonElement>());
        var updated = await Success(alice, "save_my_prediction", new { gameId = 1, homeGoals = 3, awayGoals = 1 });
        updated.GetRawText().Should().Be(persisted.GetRawText());
        (await Success(alice, "get_game_predictions", new { gameId = 1 })).GetProperty("items").EnumerateArray().Should().BeEmpty();
        (await rest.GetFromJsonAsync<JsonElement>("/api/games/1/predictions")).EnumerateArray().Should().BeEmpty();
        using var scope = host.Server.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Predictions.Count().Should().Be(2);
    }

    [Fact]
    public async Task DeadlineBoundariesAndMovedFixtures_KeepPrivacyAndRestRules()
    {
        using var host = await Host.Create();
        await using var client = await host.Connect("alice", [McpScopes.AppRead, McpScopes.PredictionsWrite]);
        var deadline = host.Clock.Now.AddHours(1);
        host.Clock.Now = deadline.AddTicks(-1);
        await Success(client, "save_my_prediction", new { gameId = 1, homeGoals = 1, awayGoals = 1 });
        using var rest = await host.Rest("alice");
        foreach (var time in new[] { deadline, deadline.AddTicks(1) })
        {
            host.Clock.Now = time;
            await Failure(client, "save_my_prediction", new { gameId = 1, homeGoals = 2, awayGoals = 2 }, "closed");
            (await rest.PostAsJsonAsync("/api/games/1/predictions", new { homeGoals = 2, awayGoals = 2 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        // Kickoff moved later but the original deadline remains closed; opponents stay hidden.
        await host.ChangeDb(db => { db.Games.Single(g => g.Id == 1).StartTime = deadline.AddDays(1).UtcDateTime; });
        await Failure(client, "save_my_prediction", new { gameId = 1, homeGoals = 2, awayGoals = 2 }, "closed");
        (await Success(client, "get_game_predictions", new { gameId = 1 })).GetProperty("items").EnumerateArray().Should().BeEmpty();
        host.Clock.Now = deadline.AddDays(1);
        (await Success(client, "get_game_predictions", new { gameId = 1 })).GetProperty("items").EnumerateArray().Should().ContainSingle();
    }

    [Fact]
    public async Task ScopeChecks_Revocation_Expiry_AndSessionIdsCannotBypassHttpAuthentication()
    {
        using var host = await Host.Create();
        await using var read = await host.Connect("alice", [McpScopes.AppRead]);
        await Failure(read, "save_my_prediction", new { gameId = 1, homeGoals = 1, awayGoals = 1 }, "predictions:write");
        await using var write = await host.Connect("bob", [McpScopes.PredictionsWrite]);
        await Failure(write, "get_current_user", new { }, "app:read");
        await Success(write, "save_my_prediction", new { gameId = 1, homeGoals = 1, awayGoals = 1 });
        var token = await host.Token("alice", [McpScopes.AppRead]);
        using var http = host.Server.CreateClient();
        http.DefaultRequestHeaders.Add("Mcp-Session-Id", "pretend-retained-session");
        foreach (var credential in new string?[] { null, "invalid", await host.Jwt("alice") })
        {
            http.DefaultRequestHeaders.Authorization = credential is null ? null : new("Bearer", credential);
            (await http.PostAsJsonAsync("/mcp", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        http.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
        await host.ChangeDb(db => db.McpAccessTokens.Single(t => t.Id == token.Connection.Id).RevokedAt = host.Clock.Now);
        (await http.PostAsJsonAsync("/mcp", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var expires = await host.Token("alice", [McpScopes.AppRead]);
        http.DefaultRequestHeaders.Authorization = new("Bearer", expires.Token);
        host.Clock.Now = expires.Connection.ExpiresAt;
        (await http.PostAsJsonAsync("/mcp", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // The already-initialized SDK connection also authenticates its next request.
        Func<Task> expiredCall = async () => await read.CallToolAsync("get_current_user");
        await expiredCall.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Pagination_ValidatesLimitsAndReachesEveryRow_AndOriginIsExact()
    {
        using var host = await Host.Create();
        await host.ChangeDb(db => db.Tournaments.AddRange(Enumerable.Range(2, 119).Reverse().Select(i => DbContextFactory.MakeTournament(i))));
        await using var client = await host.Connect("alice", [McpScopes.AppRead]);
        var ids = new List<int>();
        int? offset = 0;
        while (offset is not null)
        {
            var page = await Success(client, "list_tournaments", new { offset });
            page.GetProperty("limit").GetInt32().Should().Be(50);
            ids.AddRange(page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));
            offset = page.GetProperty("nextOffset").ValueKind == JsonValueKind.Null ? null : page.GetProperty("nextOffset").GetInt32();
        }
        ids.Should().Equal(Enumerable.Range(1, 120));
        (await Success(client, "list_tournaments", new { limit = 100 })).GetProperty("items").EnumerateArray().Should().HaveCount(100);
        (await Success(client, "list_tournaments", new { offset = int.MaxValue })).GetProperty("items").EnumerateArray().Should().BeEmpty();
        foreach (var args in new object[] { new { limit = 0 }, new { limit = 101 }, new { offset = -1 } })
            await Failure(client, "list_tournaments", args);
        using var http = host.Server.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", (await host.Token("alice", [McpScopes.AppRead])).Token);
        foreach (var origin in new[] { "https://untrusted.example", "null", "https://predictions-project.vercel.app.evil.example" })
        {
            http.DefaultRequestHeaders.Remove("Origin");
            http.DefaultRequestHeaders.Add("Origin", origin);
            (await http.PostAsJsonAsync("/mcp", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        http.DefaultRequestHeaders.Remove("Origin");
        http.DefaultRequestHeaders.Add("Origin", "https://predictions-project.vercel.app");
        var response = await http.PostAsJsonAsync("/mcp", new { });
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidArguments_AreUsefulAndDoNotMutate_AndBackendErrorsAreSanitized()
    {
        using var host = await Host.Create();
        await using var client = await host.Connect("alice", [McpScopes.AppRead, McpScopes.PredictionsWrite]);
        foreach (var args in new object[] { new { gameId = 1, homeGoals = -1, awayGoals = 0 }, new { gameId = 1, homeGoals = 1.5, awayGoals = 0 },
            new { gameId = 1, homeGoals = "2", awayGoals = 0 }, new { gameId = 1, awayGoals = 0 },
            new { gameId = 1, homeGoals = 1, awayGoals = 0, userId = "bob" }, new { gameId = 0, homeGoals = 1, awayGoals = 0 } })
            await Failure(client, "save_my_prediction", args);
        await Failure(client, "save_my_prediction", new { gameId = 999, homeGoals = 1, awayGoals = 0 }, "not found");
        await Failure(client, "get_tournament", new { tournamentId = 999 }, "not found");
        await Failure(client, "get_game", new { tournamentId = 2, gameId = 1 }, "not found");
        await Failure(client, "get_game_predictions", new { gameId = 999 }, "not found");
        await Failure(client, "get_user_prediction_details", new { userDisplayName = "alice", type = "invalid" }, "type");
        host.Football.Setup(s => s.GetCompetitionStandingsAsync(1)).ThrowsAsync(new Exception("sensitive-backend-password"));
        await Failure(client, "get_football_standings", new { tournamentId = 1 }, "could not complete");
        string.Join("\n", host.Logs.Entries.Select(e => e.Text)).Should().NotContain("sensitive-backend-password");
        using var scope = host.Server.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Predictions.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditHasMetadataOnly_ForSuccessRejectionAndDenial_AndDoesNotRetry()
    {
        using var host = await Host.Create();
        var credential = await host.Token("alice", [McpScopes.PredictionsWrite]);
        await using var client = await host.ConnectToken(credential.Token);
        await Success(client, "save_my_prediction", new { gameId = 1, homeGoals = 3, awayGoals = 2 });
        await Failure(client, "save_my_prediction", new { gameId = 1, homeGoals = -1, awayGoals = 2 });
        await using var read = await host.Connect("bob", [McpScopes.AppRead]);
        await Failure(read, "save_my_prediction", new { gameId = 1, homeGoals = 2, awayGoals = 2 });
        var events = host.Logs.Entries.Where(e => e.Category == "Predictions.Mcp.Audit").ToArray();
        events.Should().HaveCount(3);
        events.Select(e => e.Fields["Result"]).Should().Equal("succeeded", "rejected", "denied");
        foreach (var entry in events)
        {
            entry.Fields.Keys.Should().Contain(new[] { "ActorId", "TokenId", "Tool", "TargetId", "Timestamp", "Result", "CorrelationId" });
            entry.Fields["CorrelationId"].Should().NotBeNull();
            entry.Text.Should().NotContain(credential.Token).And.NotContain("homeGoals").And.NotContain("awayGoals");
        }
        events[0].Fields["TokenId"].Should().Be(credential.Connection.Id.ToString());
        string.Join("\n", host.Logs.Entries.Select(e => e.Text)).Should().NotContain(credential.Token);
    }

    [Fact]
    public async Task NonEmptyReadTools_MatchRest_AndFootballPagesFlattenAllGroups()
    {
        using var host = await Host.Create();
        await host.ChangeDb(db =>
        {
            db.Games.Add(DbContextFactory.MakeGame(2, 1, DateTime.UtcNow.AddDays(-1), 2, 1));
            db.Predictions.AddRange(DbContextFactory.MakePrediction(1, 2, "alice", 2, 1),
                DbContextFactory.MakePrediction(2, 2, "bob", 1, 0));
        });
        host.Football.Setup(f => f.GetCompetitionStandingsAsync(1)).ReturnsAsync(new CompetitionStandingsResponse
        {
            Groups = [new() { Stage = "GROUP_STAGE", Group = "B", Table = [new() { Position = 1, TeamName = "B team" }] },
                new() { Stage = "GROUP_STAGE", Group = "A", Table = [new() { Position = 1, TeamName = "A team" }] }]
        });
        await using var client = await host.Connect("alice", [McpScopes.AppRead]);
        using var rest = await host.Rest("alice");
        foreach (var (tool, args, path) in new (string, object, string)[] {
            ("get_my_predictions", new { tournamentId = 1 }, "/api/tournaments/1/my-predictions"),
            ("get_game_predictions", new { gameId = 2 }, "/api/games/2/predictions"),
            ("get_tournament_standings", new { tournamentId = 1 }, "/api/tournaments/1/standings"),
            ("get_global_standings", new { }, "/api/standings/global"),
            ("get_user_prediction_details", new { userDisplayName = "alice", tournamentId = 1, type = "scores" }, "/api/tournaments/1/standings/alice/predictions?type=scores"),
            ("get_user_prediction_details", new { userDisplayName = "bob", type = "outcomes" }, "/api/standings/global/bob/predictions?type=outcomes") })
        {
            var mcp = (await Success(client, tool, args)).GetProperty("items").EnumerateArray().ToArray();
            mcp.Should().NotBeEmpty();
            var expected = (await rest.GetFromJsonAsync<JsonElement>(path)).EnumerateArray().ToArray();
            // The SDK omits nullable fields; compare deserialized DTO values rather than JSON formatting.
            var normalized = mcp.Select(x => x.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null)
                .ToDictionary(p => p.Name, p => p.Value.ToString())).ToArray();
            var restNormalized = expected.Select(x => x.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null)
                .ToDictionary(p => p.Name, p => p.Value.ToString())).ToArray();
            normalized.Should().BeEquivalentTo(restNormalized);
        }
        var first = await Success(client, "get_football_standings", new { tournamentId = 1, limit = 1 });
        first.GetProperty("total").GetInt32().Should().Be(2);
        first.GetProperty("items")[0].GetProperty("group").GetString().Should().Be("A");
        var second = await Success(client, "get_football_standings", new { tournamentId = 1, limit = 1, offset = first.GetProperty("nextOffset").GetInt32() });
        second.GetProperty("items")[0].GetProperty("group").GetString().Should().Be("B");
        second.GetProperty("nextOffset").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AlreadyConnectedClient_IsRejectedAfterRevocation_AndCurrentRolesRefresh()
    {
        using var host = await Host.Create();
        var token = await host.Token("alice", [McpScopes.AppRead]);
        await using var client = await host.ConnectToken(token.Token);
        await Success(client, "get_current_user");
        using (var scope = host.Server.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded.Should().BeTrue();
            (await users.AddToRoleAsync((await users.FindByIdAsync("alice"))!, "Admin")).Succeeded.Should().BeTrue();
        }
        var user = await Success(client, "get_current_user");
        user.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).Should().Equal("Admin");
        user.GetProperty("scopes").EnumerateArray().Select(x => x.GetString()).Should().Equal(McpScopes.AppRead);
        await host.ChangeDb(db => db.McpAccessTokens.Single(t => t.Id == token.Connection.Id).RevokedAt = host.Clock.Now);
        Func<Task> revokedCall = async () => await client.CallToolAsync("get_current_user");
        await revokedCall.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task FailedMutation_IsSanitizedAuditedAndNeverRetried()
    {
        var service = new Mock<IPredictionService>();
        service.Setup(p => p.PlacePredictionAsync(1, "alice", It.IsAny<PredictionsAPI.DTOs.Predictions.PlacePredictionRequest>()))
            .ThrowsAsync(new Exception("sensitive-backend-password"));
        using var host = await Host.Create(services => services.AddScoped<IPredictionService>(_ => service.Object));
        await using var client = await host.Connect("alice", [McpScopes.PredictionsWrite]);
        await Failure(client, "save_my_prediction", new { gameId = 1, homeGoals = 1, awayGoals = 0 }, "could not complete");
        service.Verify(p => p.PlacePredictionAsync(1, "alice", It.IsAny<PredictionsAPI.DTOs.Predictions.PlacePredictionRequest>()), Times.Once);
        host.Logs.Entries.Single(e => e.Category == "Predictions.Mcp.Audit").Fields["Result"].Should().Be("failed");
        string.Join("\n", host.Logs.Entries.Select(e => e.Text)).Should().NotContain("sensitive-backend-password");
    }

    private static Dictionary<string, object?> Args(object? args) => args is null ? [] :
        JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(args))!;
    private static async Task<JsonElement> Success(McpClient client, string tool, object? args = null)
    {
        var result = await client.CallToolAsync(tool, Args(args));
        result.IsError.Should().NotBeTrue(string.Join(" ", result.Content.OfType<TextContentBlock>().Select(c => c.Text)));
        result.StructuredContent.Should().NotBeNull();
        return JsonSerializer.SerializeToElement(result.StructuredContent);
    }
    private static async Task Failure(McpClient client, string tool, object args, string? contains = null)
    {
        var result = await client.CallToolAsync(tool, Args(args));
        result.IsError.Should().BeTrue();
        var message = string.Join(" ", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        if (contains is not null) message.Should().Contain(contains);
        message.Should().NotContain("sensitive-backend-password").And.NotContain("System.").And.NotContain(" at ");
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Host : IDisposable
    {
        public TestServer Server { get; private set; } = null!;
        public Clock Clock { get; } = new();
        public Logs Logs { get; } = new();
        public Mock<IFootballSyncService> Football { get; } = new();
        public static async Task<Host> Create(Action<IServiceCollection>? configure = null)
        {
            var host = new Host();
            var name = Guid.NewGuid().ToString();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["JwtSettings:SecretKey"] = "Integration-only-signing-key-not-a-production-secret-12345678",
                ["JwtSettings:Issuer"] = "Tests", ["JwtSettings:Audience"] = "Tests", ["JwtSettings:ExpirationInMinutes"] = "60",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused", ["FootballApi:BaseUrl"] = "https://example.invalid/",
                ["Mcp:AllowedOrigins:0"] = "https://predictions-project.vercel.app"
            }).Build();
            host.Football.Setup(s => s.GetCompetitionStandingsAsync(It.IsAny<int>())).ReturnsAsync((CompetitionStandingsResponse?)null);
            host.Server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(config);
                services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(host.Logs));
                services.AddPredictionsServices(config);
                services.RemoveAll<AppDbContext>();
                services.AddScoped(_ => DbContextFactory.Create(name));
                services.RemoveAll<IHostedService>();
                services.AddSingleton<TimeProvider>(host.Clock);
                services.AddScoped<IFootballSyncService>(_ => host.Football.Object);
                configure?.Invoke(services);
                services.AddPredictionsMcp();
                services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
            }).Configure(app =>
            {
                app.UseRouting();
                app.UsePredictionsMcpOriginValidation();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(e => { e.MapControllers(); e.MapPredictionsMcp(); });
            }));
            using var scope = host.Server.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var username in new[] { "alice", "bob" })
                (await users.CreateAsync(DbContextFactory.MakeUser(username, username), "TestPass123!")).Succeeded.Should().BeTrue();
            await host.ChangeDb(db =>
            {
                db.Tournaments.Add(DbContextFactory.MakeTournament(1));
                db.Games.Add(DbContextFactory.MakeGame(1, 1, host.Clock.Now.AddHours(1).UtcDateTime, predictionDeadline: host.Clock.Now.AddHours(1).UtcDateTime));
            });
            return host;
        }
        public async Task ChangeDb(Action<AppDbContext> change)
        {
            using var scope = Server.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            change(db);
            await db.SaveChangesAsync();
        }
        public async Task<CreatedMcpAccessTokenResponse> Token(string user, string[] scopes)
        {
            using var scope = Server.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<IMcpAccessTokenService>()
                .CreateAsync(user, new CreateMcpAccessTokenRequest { Name = "Test", Scopes = scopes }))!;
        }
        public async Task<McpClient> Connect(string user, string[] scopes, string version = "2025-11-25") =>
            await ConnectToken((await Token(user, scopes)).Token, version);
        public async Task<McpClient> ConnectToken(string token, string version = "2025-11-25")
        {
            var http = Server.CreateClient();
            http.DefaultRequestHeaders.Authorization = new("Bearer", token);
            var transport = new HttpClientTransport(new() { Endpoint = new Uri("http://localhost/mcp"), TransportMode = HttpTransportMode.StreamableHttp }, http, ownsHttpClient: true);
            return await McpClient.CreateAsync(transport, new() { ProtocolVersion = version, InitializationTimeout = TimeSpan.FromSeconds(10) });
        }
        public async Task<string> Jwt(string user)
        {
            using var http = Server.CreateClient();
            var response = await http.PostAsJsonAsync("/api/auth/login", new { email = $"{user}@test.com", password = "TestPass123!" });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.Token;
        }
        public async Task<HttpClient> Rest(string user)
        {
            var http = Server.CreateClient();
            http.DefaultRequestHeaders.Authorization = new("Bearer", await Jwt(user));
            return http;
        }
        public void Dispose() => Server.Dispose();
    }
    private sealed record LogEntry(string Category, string Text, Dictionary<string, object?> Fields);
    private sealed class Logs : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Entries);
        public void Dispose() { }
        private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new(category, formatter(state, exception), state is IEnumerable<KeyValuePair<string, object?>> values ? values.ToDictionary(x => x.Key, x => x.Value) : []));
        }
    }
}
