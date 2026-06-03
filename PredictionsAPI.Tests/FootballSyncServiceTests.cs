using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PredictionsAPI.Entities;
using PredictionsAPI.FootballApi;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class FootballSyncServiceTests
{
    [Fact]
    public async Task BackfillFixtures_AddsOnlyMissingProviderFixtures_AndPreservesExistingPredictions()
    {
        var db = DbContextFactory.Create(nameof(BackfillFixtures_AddsOnlyMissingProviderFixtures_AndPreservesExistingPredictions));
        db.Users.Add(DbContextFactory.MakeUser("u1", "Alice"));
        db.Tournaments.Add(new Tournament
        {
            Id = 1,
            Name = "World Cup",
            CreatedAt = DateTime.UtcNow,
            ExternalLeagueId = 2000,
            ExternalSeason = 2026
        });
        db.Games.Add(new Game
        {
            Id = 1,
            TournamentId = 1,
            HomeTeam = "Mexico",
            AwayTeam = "South Africa",
            StartTime = new DateTime(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc),
            ExternalFixtureId = 1001
        });
        db.Predictions.Add(DbContextFactory.MakePrediction(1, 1, "u1", 2, 1));
        await db.SaveChangesAsync();

        var service = MakeService(db, MatchesJson("""
        [
          { "id": 1001, "utcDate": "2026-06-11T19:00:00Z", "status": "TIMED", "homeTeam": { "name": "Mexico", "shortName": "Mexico", "tla": "MEX", "crest": "mexico.svg" }, "awayTeam": { "name": "South Africa", "shortName": "South Africa", "tla": "RSA", "crest": "south-africa.svg" }, "score": { "duration": "REGULAR", "fullTime": { "home": null, "away": null } } },
          { "id": 1002, "utcDate": "2026-06-12T19:00:00Z", "status": "TIMED", "homeTeam": { "name": "Canada", "shortName": "Canada", "tla": "CAN", "crest": "canada.svg" }, "awayTeam": { "name": "Qatar", "shortName": "Qatar", "tla": "QAT", "crest": "qatar.svg" }, "score": { "duration": "REGULAR", "fullTime": { "home": null, "away": null } } }
        ]
        """));

        var result = await service.BackfillFixturesAsync(1);

        result.ProviderFixtures.Should().Be(2);
        result.ExistingGames.Should().Be(1);
        result.Added.Should().Be(1);
        result.SkippedExisting.Should().Be(1);
        result.MatchedExisting.Should().Be(0);
        db.Games.Should().HaveCount(2);
        db.Games.Should().Contain(g => g.ExternalFixtureId == 1002 && g.HomeTeam == "Canada" && g.AwayTeam == "Qatar");
        db.Predictions.Should().ContainSingle(p => p.GameId == 1 && p.UserId == "u1");
    }

    [Fact]
    public async Task BackfillFixtures_AttachesExternalFixtureIdToMatchingExistingGameWithoutDuplicatingIt()
    {
        var db = DbContextFactory.Create(nameof(BackfillFixtures_AttachesExternalFixtureIdToMatchingExistingGameWithoutDuplicatingIt));
        db.Tournaments.Add(new Tournament
        {
            Id = 1,
            Name = "World Cup",
            CreatedAt = DateTime.UtcNow,
            ExternalLeagueId = 2000,
            ExternalSeason = 2026
        });
        db.Games.Add(new Game
        {
            Id = 1,
            TournamentId = 1,
            HomeTeam = "Mexico",
            AwayTeam = "South Africa",
            StartTime = new DateTime(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc),
            ExternalFixtureId = null
        });
        await db.SaveChangesAsync();

        var service = MakeService(db, MatchesJson("""
        [
          { "id": 1001, "utcDate": "2026-06-11T19:00:00Z", "status": "TIMED", "homeTeam": { "name": "Mexico", "shortName": "Mexico", "tla": "MEX", "crest": "mexico.svg" }, "awayTeam": { "name": "South Africa", "shortName": "South Africa", "tla": "RSA", "crest": "south-africa.svg" }, "score": { "duration": "REGULAR", "fullTime": { "home": null, "away": null } } }
        ]
        """));

        var result = await service.BackfillFixturesAsync(1);

        result.Added.Should().Be(0);
        result.MatchedExisting.Should().Be(1);
        db.Games.Should().ContainSingle();
        db.Games.Single().ExternalFixtureId.Should().Be(1001);
        db.Games.Single().HomeTeamShort.Should().Be("MEX");
        db.Games.Single().AwayTeamShort.Should().Be("RSA");
    }

    private static FootballSyncService MakeService(PredictionsAPI.Data.AppDbContext db, string matchesJson)
    {
        var handler = new StubHttpMessageHandler(matchesJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.football-data.org/v4/") };
        var client = new FootballApiClient(http, new FootballApiStatusStore(), NullLogger<FootballApiClient>.Instance);
        return new FootballSyncService(client, db, NullLogger<FootballSyncService>.Instance);
    }

    private static string MatchesJson(string matchesJson) => $$"""{ "matches": {{matchesJson}} }""";

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _matchesJson;

        public StubHttpMessageHandler(string matchesJson)
        {
            _matchesJson = matchesJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_matchesJson)
            };
            response.Headers.Add("X-Requests-Available-Minute", "9");
            return Task.FromResult(response);
        }
    }
}
