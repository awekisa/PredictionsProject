using FluentAssertions;
using PredictionsAPI.DTOs.Predictions;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class PredictionServiceTests
{
    [Fact]
    public async Task PlacePrediction_BeforeStartTime_Succeeds()
    {
        var db = DbContextFactory.Create(nameof(PlacePrediction_BeforeStartTime_Succeeds));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        // Use AddDays(1) so it's safely in the future across all timezones
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(1));

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.PlacePredictionAsync(1, "u1", new PlacePredictionRequest
        {
            HomeGoals = 2,
            AwayGoals = 1
        });

        result.Should().NotBeNull();
        result!.HomeGoals.Should().Be(2);
        result.AwayGoals.Should().Be(1);
    }

    [Fact]
    public async Task PlacePrediction_AfterStartTime_ReturnsNull()
    {
        var db = DbContextFactory.Create(nameof(PlacePrediction_AfterStartTime_ReturnsNull));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1));

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.PlacePredictionAsync(1, "u1", new PlacePredictionRequest
        {
            HomeGoals = 2,
            AwayGoals = 1
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task PlacePrediction_GameNotFound_ReturnsNull()
    {
        var db = DbContextFactory.Create(nameof(PlacePrediction_GameNotFound_ReturnsNull));
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.PlacePredictionAsync(999, "u1", new PlacePredictionRequest
        {
            HomeGoals = 1,
            AwayGoals = 0
        });

        result.Should().BeNull();
    }

    [Fact]
    public async Task PlacePrediction_ExistingPrediction_UpdatesInPlace()
    {
        var db = DbContextFactory.Create(nameof(PlacePrediction_ExistingPrediction_UpdatesInPlace));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(1));
        var existing = DbContextFactory.MakePrediction(1, 1, "u1", 0, 0);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(existing);
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.PlacePredictionAsync(1, "u1", new PlacePredictionRequest
        {
            HomeGoals = 3,
            AwayGoals = 2
        });

        result.Should().NotBeNull();
        result!.HomeGoals.Should().Be(3);
        result.AwayGoals.Should().Be(2);

        // Only one prediction should exist for this user/game
        var predictionCount = db.Predictions.Count(p => p.GameId == 1 && p.UserId == "u1");
        predictionCount.Should().Be(1);
    }

    [Fact]
    public async Task GetGamePredictions_BeforeStartTime_ReturnsEmpty()
    {
        var db = DbContextFactory.Create(nameof(GetGamePredictions_BeforeStartTime_ReturnsEmpty));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        // Game hasn't started yet — use AddDays(1) to be safely in the future across all timezones
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(1));
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 2, 1);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.GetGamePredictionsAsync(1);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGamePredictions_AfterStartTime_ReturnsPredictions()
    {
        var db = DbContextFactory.Create(nameof(GetGamePredictions_AfterStartTime_ReturnsPredictions));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddHours(-1));
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 2, 1);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.GetGamePredictionsAsync(1);

        result.Should().HaveCount(1);
        result[0].HomeGoals.Should().Be(2);
        result[0].AwayGoals.Should().Be(1);
        result[0].UserDisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetGamePredictions_GameNotFound_ReturnsEmpty()
    {
        var db = DbContextFactory.Create(nameof(GetGamePredictions_GameNotFound_ReturnsEmpty));
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.GetGamePredictionsAsync(999);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyPredictions_ReturnsOnlyUserPredictions()
    {
        var db = DbContextFactory.Create(nameof(GetMyPredictions_ReturnsOnlyUserPredictions));
        var alice = DbContextFactory.MakeUser("u1", "Alice");
        var bob = DbContextFactory.MakeUser("u2", "Bob");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1));

        db.Users.AddRange(alice, bob);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(DbContextFactory.MakePrediction(1, 1, "u1", 2, 1));
        db.Predictions.Add(DbContextFactory.MakePrediction(2, 1, "u2", 0, 0));
        await db.SaveChangesAsync();

        var service = new PredictionService(db);
        var result = await service.GetMyPredictionsAsync(1, "u1");

        result.Should().HaveCount(1);
        result[0].UserDisplayName.Should().Be("Alice");
    }
}
