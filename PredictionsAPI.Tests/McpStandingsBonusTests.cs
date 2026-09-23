using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PredictionsAPI.Security;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public partial class McpProtocolTests
{
    [Fact]
    public async Task StandingsBonus_RestAndMcpAgree_WhileMcpPagesRemainAlphabetical()
    {
        using var host = await Host.Create();
        await host.ChangeDb(db =>
        {
            for (int id = 2; id <= 6; id++)
            {
                db.Games.Add(DbContextFactory.MakeGame(id, 1, DateTime.UtcNow.AddDays(-2), 2, 1));
                db.Predictions.Add(DbContextFactory.MakePrediction(id, id, "bob", 1, 0));
                if (id <= 3)
                    db.Predictions.Add(DbContextFactory.MakePrediction(id + 10, id, "alice", 2, 1));
            }
        });
        using var rest = await host.Rest("alice");
        await using var client = await host.Connect("alice", [McpScopes.AppRead]);
        var tools = await client.ListToolsAsync();
        foreach (var (tool, path) in new[] {
            ("get_tournament_standings", "/api/tournaments/1/standings"),
            ("get_global_standings", "/api/standings/global") })
        {
            var schema = tools.Single(t => t.Name == tool).ProtocolTool.OutputSchema!.Value;
            schema.GetProperty("properties").GetProperty("items").GetProperty("items")
                .GetProperty("properties").GetProperty("bonusPoints").GetProperty("type")
                .GetString().Should().Be("integer");
            var rows = (await rest.GetFromJsonAsync<JsonElement>(path)).EnumerateArray().ToArray();
            rows.Select(r => r.GetProperty("userDisplayName").GetString()).Should().Equal("bob", "alice");
            rows.Select(r => r.GetProperty("points").GetInt32()).Should().Equal(7, 6);
            rows.Select(r => r.GetProperty("bonusPoints").GetInt32()).Should().Equal(2, 0);
            rows.Select(r => r.GetProperty("position").GetInt32()).Should().Equal(1, 2);
            for (int offset = 0; offset < 2; offset++)
            {
                object args = tool == "get_tournament_standings"
                    ? new { tournamentId = 1, offset, limit = 1 }
                    : new { offset, limit = 1 };
                var page = await Success(client, tool, args);
                var row = page.GetProperty("items").EnumerateArray().Single();
                var expected = rows[1 - offset];
                row.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString())
                    .Should().BeEquivalentTo(expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString()));
            }
        }
    }
}
