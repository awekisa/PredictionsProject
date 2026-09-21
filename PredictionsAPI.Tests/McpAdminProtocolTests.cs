using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using Moq;
using Npgsql;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Football;
using PredictionsAPI.DTOs.Tournaments;
using PredictionsAPI.FootballApi;
using PredictionsAPI.Security;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Services.Interfaces;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public partial class McpProtocolTests
{
    private static readonly string[] AdminReadNames = ["admin_list_users", "admin_list_predictions", "admin_get_football_status", "admin_list_football_leagues"];
    private static readonly string[] AdminToolNames = AdminCases().Select(c => (string)c[0]).ToArray();
    public static IEnumerable<object[]> AdminCases()
    {
        yield return ["admin_list_users", new { }];
        yield return ["admin_list_predictions", new { }];
        yield return ["admin_get_football_status", new { }];
        yield return ["admin_list_football_leagues", new { }];
        yield return ["admin_create_tournament", new { request = new { name = "New Cup" } }];
        yield return ["admin_update_tournament", new { tournamentId = 1, request = new { name = "Updated Cup" } }];
        yield return ["admin_delete_tournament", new { tournamentId = 1 }];
        yield return ["admin_create_game", new { tournamentId = 1, request = new { homeTeam = "Home", awayTeam = "Away", startTime = "2030-01-01T12:00:00Z" } }];
        yield return ["admin_update_game", new { tournamentId = 1, gameId = 1, request = new { homeTeam = "Home", awayTeam = "Away", startTime = "2030-01-01T12:00:00Z" } }];
        yield return ["admin_delete_game", new { tournamentId = 1, gameId = 1 }];
        yield return ["admin_set_game_result", new { gameId = 2, request = new { homeGoals = 2, awayGoals = 1 } }];
        yield return ["admin_sync_game_score", new { gameId = 2, request = new { homeGoals = 2, awayGoals = 1, isFinished = true, fifaMatchStatus = 3, fifaMatchTime = "FT" } }];
        yield return ["admin_clear_game_result", new { gameId = 2 }];
        yield return ["admin_import_league", new { request = new { leagueId = 10, season = 2026, name = "Imported" } }];
        yield return ["admin_backfill_fixtures", new { tournamentId = 1 }];
        yield return ["admin_sync_tournament_scores", new { tournamentId = 1 }];
        yield return ["admin_delete_user", new { userId = "bob" }];
        yield return ["admin_delete_prediction", new { predictionId = 1 }];
    }

    [Theory]
    [MemberData(nameof(AdminCases))]
    public async Task EveryAdminTool_EnforcesScopeAndLiveRole_ThenPermitsAdmin(string tool, object arguments)
    {
        using var host = await AdminHost();
        var required = AdminReadNames.Contains(tool) ? McpScopes.AdminRead : McpScopes.AdminWrite;
        await using var regular = await host.Connect("bob", [McpScopes.AppRead, McpScopes.PredictionsWrite]);
        await Failure(regular, tool, arguments, required);
        await using var wrongScope = await host.Connect("alice", [required == McpScopes.AdminRead ? McpScopes.AdminWrite : McpScopes.AdminRead]);
        await Failure(wrongScope, tool, arguments, required);
        await using var admin = await host.Connect("alice", [required]);
        await SetAdminRole(host, false);
        await Failure(admin, tool, arguments, required);
        await SetAdminRole(host, true);
        await Success(admin, tool, arguments);
        if (required == McpScopes.AdminWrite)
        {
            var audit = host.Logs.Entries.Last(e => e.Category == "Predictions.Mcp.Audit");
            audit.Fields["Tool"].Should().Be(tool);
            audit.Fields["ActorId"].Should().Be("alice");
            audit.Fields["Result"].Should().Be("succeeded");
            audit.Fields["TargetId"].Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AdminDtoValidationAndTargetErrors_DoNotRunInvalidWrites()
    {
        using var host = await AdminHost();
        await using var admin = await host.Connect("alice", [McpScopes.AdminWrite]);
        foreach (var (tool, args) in new (string, object)[] {
            ("admin_create_tournament", new { request = new { name = " " } }),
            ("admin_create_game", new { tournamentId = 1, request = new { homeTeam = "H", awayTeam = "A" } }),
            ("admin_import_league", new { request = new { name = "No IDs" } }),
            ("admin_create_tournament", new { request = (object?)null }),
            ("admin_create_tournament", new { request = new { name = "ok", arbitrary = "bad" } }),
            ("admin_create_game", new { tournamentId = 1, request = new { homeTeam = "", awayTeam = "A", startTime = "2030-01-01T12:00:00Z" } }),
            ("admin_create_game", new { tournamentId = 1, request = new { homeTeam = "H", awayTeam = "A", startTime = "2030-01-01T12:00:00" } }),
            ("admin_create_game", new { tournamentId = 999, request = new { homeTeam = "H", awayTeam = "A", startTime = "2030-01-01T12:00:00Z" } }),
            ("admin_update_tournament", new { tournamentId = 999, request = new { name = "Name" } }),
            ("admin_delete_tournament", new { tournamentId = -1 }),
            ("admin_update_game", new { tournamentId = 999, gameId = 1, request = new { homeTeam = "H", awayTeam = "A", startTime = "2030-01-01T12:00:00Z" } }),
            ("admin_delete_game", new { tournamentId = 999, gameId = 1 }),
            ("admin_set_game_result", new { gameId = 1, request = new { homeGoals = 1, awayGoals = 0 } }),
            ("admin_set_game_result", new { gameId = 2, request = new { homeGoals = -1, awayGoals = 0 } }),
            ("admin_sync_game_score", new { gameId = 2, request = new { homeGoals = 1, isFinished = true } }),
            ("admin_sync_game_score", new { gameId = 2, request = new { isFinished = "true" } }),
            ("admin_clear_game_result", new { gameId = 999 }),
            ("admin_backfill_fixtures", new { tournamentId = 999 }),
            ("admin_sync_tournament_scores", new { tournamentId = 999 }),
            ("admin_delete_user", new { userId = "" }),
            ("admin_delete_user", new { userId = "missing" }),
            ("admin_delete_prediction", new { predictionId = 999 }) })
            await Failure(admin, tool, args);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tournaments.Count().Should().Be(1);
        db.Games.Count().Should().Be(2);
        db.Predictions.Count().Should().Be(1);
        host.Football.Verify(f => f.SyncScoresAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AdminResultAndDeletionChanges_RecalculateStandings_AndMovedKickoffKeepsDeadline()
    {
        using var host = await AdminHost();
        await using var admin = await host.Connect("alice", [McpScopes.AppRead, McpScopes.AdminWrite]);
        await Success(admin, "admin_set_game_result", new { gameId = 2, request = new { homeGoals = 2, awayGoals = 1 } });
        (await Success(admin, "get_global_standings")).GetProperty("items")[0].GetProperty("points").GetInt32().Should().Be(3);
        await Success(admin, "admin_sync_game_score", new { gameId = 2, request = new { homeGoals = 1, awayGoals = 0, isFinished = true } });
        (await Success(admin, "get_global_standings")).GetProperty("items")[0].GetProperty("points").GetInt32().Should().Be(1);
        await Success(admin, "admin_clear_game_result", new { gameId = 2 });
        (await Success(admin, "get_global_standings")).GetProperty("items")[0].GetProperty("points").GetInt32().Should().Be(0);
        var before = await Success(admin, "get_game", new { tournamentId = 1, gameId = 1 });
        var after = await Success(admin, "admin_update_game", new { tournamentId = 1, gameId = 1, request = new { homeTeam = "H", awayTeam = "A", startTime = "2030-01-01T12:00:00Z" } });
        after.GetProperty("predictionDeadline").GetString().Should().Be(before.GetProperty("predictionDeadline").GetString());
        await Success(admin, "admin_delete_prediction", new { predictionId = 1 });
        (await Success(admin, "get_global_standings")).GetProperty("items").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task AdminReadPaginationAndEndpointSwitch_WorkWithoutChangingRest()
    {
        using var host = await AdminHost();
        await using var admin = await host.Connect("alice", [McpScopes.AdminRead]);
        var first = await Success(admin, "admin_list_users", new { limit = 1 });
        first.GetProperty("items")[0].GetProperty("id").GetString().Should().Be("alice");
        var second = await Success(admin, "admin_list_users", new { offset = 1, limit = 1 });
        second.GetProperty("items")[0].GetProperty("id").GetString().Should().Be("bob");
        await Failure(admin, "admin_list_users", new { limit = 101 });
        var config = host.Server.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        config["Mcp:Enabled"] = "false";
        using var http = host.Server.CreateClient();
        (await http.PostAsJsonAsync("/mcp", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var rest = await host.Rest("alice");
        (await rest.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);
        config["Mcp:Enabled"] = "true";
        await Success(admin, "admin_list_users");
    }

    [Fact]
    public async Task ProviderErrors_AreRedactedInRealClientLogsAndToolResponses()
    {
        using var host = await AdminHost(services =>
        {
            services.AddScoped<IFootballSyncService, FootballSyncService>();
            services.AddHttpClient<FootballApiClient>().ConfigurePrimaryHttpMessageHandler(() => new FailingProvider());
        });
        await using var admin = await host.Connect("alice", [McpScopes.AdminRead, McpScopes.AdminWrite]);
        await Failure(admin, "admin_list_football_leagues", new { }, "could not complete");
        await Failure(admin, "admin_import_league", new { request = new { leagueId = 10, season = 2026, name = "Disposable" } }, "could not complete");
        string.Join("\n", host.Logs.Entries.Select(e => e.Text)).Should().NotContain("provider-secret-value");
        host.Logs.Entries.Last(e => e.Category == "Predictions.Mcp.Audit").Fields["Result"].Should().Be("failed");
    }

    [AdminPostgresFact]
    public async Task AdminDeletionCascades_UseRealPostgres_AndDeletedOwnerCannotCallAgain()
    {
        var connection = Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES")!;
        var database = $"mcp_admin_test_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(connection);
        await adminConnection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", adminConnection)) await create.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString).Options;
        try
        {
            await using (var db = new AppDbContext(options)) await db.Database.MigrateAsync();
            using var host = await AdminHost(services => { services.RemoveAll<AppDbContext>(); services.AddScoped(_ => new AppDbContext(options)); });
            await using var client = await host.Connect("alice", [McpScopes.AdminWrite]);
            await using var bob = await host.Connect("bob", [McpScopes.AppRead]);
            await Success(client, "admin_delete_game", new { tournamentId = 1, gameId = 2 });
            await using (var db = new AppDbContext(options)) (await db.Predictions.CountAsync()).Should().Be(0);
            await host.ChangeDb(db => db.Predictions.Add(DbContextFactory.MakePrediction(2, 1, "bob", 0, 0)));
            await Success(client, "admin_delete_tournament", new { tournamentId = 1 });
            await using (var db = new AppDbContext(options))
            {
                (await db.Games.CountAsync()).Should().Be(0);
                (await db.Predictions.CountAsync()).Should().Be(0);
            }
            await Success(client, "admin_delete_user", new { userId = "bob" });
            await using (var db = new AppDbContext(options)) (await db.McpAccessTokens.AnyAsync(t => t.UserId == "bob")).Should().BeFalse();
            Func<Task> deletedCall = async () => await bob.CallToolAsync("get_current_user");
            await deletedCall.Should().ThrowAsync<Exception>();
            await Success(client, "admin_delete_user", new { userId = "alice" });
            Func<Task> deletedAdminCall = async () => await client.CallToolAsync("admin_delete_prediction", Args(new { predictionId = 999 }));
            await deletedAdminCall.Should().ThrowAsync<Exception>();
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", adminConnection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Host> AdminHost(Action<IServiceCollection>? configure = null)
    {
        var host = await Host.Create(configure);
        await SetAdminRole(host, true);
        await host.ChangeDb(db =>
        {
            db.Games.Add(DbContextFactory.MakeGame(2, 1, DateTime.UtcNow.AddDays(-1)));
            db.Predictions.Add(DbContextFactory.MakePrediction(1, 2, "bob", 2, 1));
        });
        host.Football.Setup(f => f.GetCompetitionsAsync()).ReturnsAsync([new() { LeagueId = 10, Name = "League", Seasons = [2026] }]);
        host.Football.Setup(f => f.ImportLeagueAsync(It.IsAny<ImportLeagueRequest>())).ReturnsAsync(new ImportLeagueResponse { Tournament = new TournamentResponse { Id = 42, Name = "Imported" }, GamesImported = 3 });
        host.Football.Setup(f => f.BackfillFixturesAsync(1)).ReturnsAsync(new BackfillFixturesResponse { Added = 2 });
        host.Football.Setup(f => f.SyncScoresAsync(1)).ReturnsAsync(2);
        return host;
    }
    private static async Task SetAdminRole(Host host, bool enabled)
    {
        using var scope = host.Server.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("Admin")) (await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded.Should().BeTrue();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<PredictionsAPI.Entities.ApplicationUser>>();
        var user = (await users.FindByIdAsync("alice"))!;
        (enabled ? await users.AddToRoleAsync(user, "Admin") : await users.RemoveFromRoleAsync(user, "Admin")).Succeeded.Should().BeTrue();
    }
    private sealed class FailingProvider : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("provider-secret-value") });
    }
    private sealed class AdminPostgresFactAttribute : FactAttribute
    {
        public AdminPostgresFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MCP_TEST_POSTGRES"))) Skip = "Set MCP_TEST_POSTGRES to run relational cascade checks.";
        }
    }
}
