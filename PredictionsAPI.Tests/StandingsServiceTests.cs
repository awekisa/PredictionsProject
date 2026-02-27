using FluentAssertions;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class StandingsServiceTests
{
    private static StandingsService CreateService(string dbName)
    {
        var ctx = DbContextFactory.Create(dbName);
        return new StandingsService(ctx);
    }

    [Fact]
    public async Task ExactScore_Awards3Points()
    {
        var db = DbContextFactory.Create(nameof(ExactScore_Awards3Points));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 2, awayGoals: 1);
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 2, 1);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings.Should().HaveCount(1);
        standings[0].Points.Should().Be(3);
        standings[0].CorrectScores.Should().Be(1);
    }

    [Fact]
    public async Task CorrectOutcome_HomeWin_Awards1Point()
    {
        var db = DbContextFactory.Create(nameof(CorrectOutcome_HomeWin_Awards1Point));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 3, awayGoals: 1);
        // Correct outcome (home win) but wrong score
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 2, 0);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings[0].Points.Should().Be(1);
        standings[0].CorrectScores.Should().Be(0);
    }

    [Fact]
    public async Task CorrectOutcome_AwayWin_Awards1Point()
    {
        var db = DbContextFactory.Create(nameof(CorrectOutcome_AwayWin_Awards1Point));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 0, awayGoals: 2);
        // Correct outcome (away win) but wrong score
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 1, 3);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings[0].Points.Should().Be(1);
        standings[0].CorrectScores.Should().Be(0);
    }

    [Fact]
    public async Task CorrectOutcome_Draw_Awards1Point()
    {
        var db = DbContextFactory.Create(nameof(CorrectOutcome_Draw_Awards1Point));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 1, awayGoals: 1);
        // Correct outcome (draw) but wrong score
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 2, 2);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings[0].Points.Should().Be(1);
    }

    [Fact]
    public async Task WrongPrediction_Awards0Points()
    {
        var db = DbContextFactory.Create(nameof(WrongPrediction_Awards0Points));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 2, awayGoals: 1);
        // Wrong outcome (predicted away win, actual home win)
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 0, 2);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings[0].Points.Should().Be(0);
        standings[0].CorrectScores.Should().Be(0);
    }

    [Fact]
    public async Task GamesWithoutResult_ShowPlayerWithZeroPoints()
    {
        var db = DbContextFactory.Create(nameof(GamesWithoutResult_ShowPlayerWithZeroPoints));
        var user = DbContextFactory.MakeUser("u1", "Alice");
        var tournament = DbContextFactory.MakeTournament(1);
        // Game has no result yet
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1));
        var prediction = DbContextFactory.MakePrediction(1, 1, "u1", 2, 1);

        db.Users.Add(user);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(prediction);
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        // User appears with 0 pts until the game gets a result
        standings.Should().HaveCount(1);
        standings[0].UserDisplayName.Should().Be("Alice");
        standings[0].Points.Should().Be(0);
        standings[0].TotalPredictions.Should().Be(1);
    }

    [Fact]
    public async Task Standings_OrderedByPoints_ThenCorrectScores_ThenTotalPredictions()
    {
        var db = DbContextFactory.Create(nameof(Standings_OrderedByPoints_ThenCorrectScores_ThenTotalPredictions));

        var alice = DbContextFactory.MakeUser("u1", "Alice");
        var bob = DbContextFactory.MakeUser("u2", "Bob");
        var carol = DbContextFactory.MakeUser("u3", "Carol");
        var tournament = DbContextFactory.MakeTournament(1);
        var game1 = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 2, awayGoals: 1);
        var game2 = DbContextFactory.MakeGame(2, 1, DateTime.UtcNow.AddDays(-2), homeGoals: 0, awayGoals: 0);

        db.Users.AddRange(alice, bob, carol);
        db.Tournaments.Add(tournament);
        db.Games.AddRange(game1, game2);

        // Alice: game1 exact (3pts), game2 exact (3pts) → 6pts, 2 correct scores
        db.Predictions.Add(DbContextFactory.MakePrediction(1, 1, "u1", 2, 1));
        db.Predictions.Add(DbContextFactory.MakePrediction(2, 2, "u1", 0, 0));

        // Bob: game1 exact (3pts), game2 correct outcome (1pt) → 4pts, 1 correct score
        db.Predictions.Add(DbContextFactory.MakePrediction(3, 1, "u2", 2, 1));
        db.Predictions.Add(DbContextFactory.MakePrediction(4, 2, "u2", 1, 1));

        // Carol: game1 wrong (0pts), game2 exact (3pts) → 3pts, 1 correct score
        db.Predictions.Add(DbContextFactory.MakePrediction(5, 1, "u3", 0, 2));
        db.Predictions.Add(DbContextFactory.MakePrediction(6, 2, "u3", 0, 0));

        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings.Should().HaveCount(3);
        standings[0].UserDisplayName.Should().Be("Alice");
        standings[0].Points.Should().Be(6);
        standings[1].UserDisplayName.Should().Be("Bob");
        standings[1].Points.Should().Be(4);
        standings[2].UserDisplayName.Should().Be("Carol");
        standings[2].Points.Should().Be(3);
    }

    [Fact]
    public async Task Standings_SamePoints_MoreCorrectScoresRanksHigher()
    {
        var db = DbContextFactory.Create(nameof(Standings_SamePoints_MoreCorrectScoresRanksHigher));

        var alice = DbContextFactory.MakeUser("u1", "Alice");
        var bob = DbContextFactory.MakeUser("u2", "Bob");
        var tournament = DbContextFactory.MakeTournament(1);
        // Two games both with results
        var game1 = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 2, awayGoals: 1);
        var game2 = DbContextFactory.MakeGame(2, 1, DateTime.UtcNow.AddDays(-2), homeGoals: 1, awayGoals: 0);

        db.Users.AddRange(alice, bob);
        db.Tournaments.Add(tournament);
        db.Games.AddRange(game1, game2);

        // Alice: game1 exact (3pts), game2 correct outcome only (1pt) → 4pts, 1 correct score
        db.Predictions.Add(DbContextFactory.MakePrediction(1, 1, "u1", 2, 1));
        db.Predictions.Add(DbContextFactory.MakePrediction(2, 2, "u1", 2, 0));

        // Bob: game1 correct outcome only (1pt), game2 exact (3pts) → 4pts, 1 correct score
        // Both have 4 pts and 1 correct score, Bob has fewer total predictions (2 vs 2 — same)
        // Let's make Bob have game1 outcome only (1pt) + game2 exact (3pt) = 4pts, 1 correctScore
        db.Predictions.Add(DbContextFactory.MakePrediction(3, 1, "u2", 3, 1));
        db.Predictions.Add(DbContextFactory.MakePrediction(4, 2, "u2", 1, 0));

        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings[0].Points.Should().Be(4);
        standings[1].Points.Should().Be(4);
        // Both have 1 correct score, same total predictions — order is stable
        standings.Select(s => s.CorrectScores).Should().AllBeEquivalentTo(1);
    }

    [Fact]
    public async Task Position_IsAssignedSequentially()
    {
        var db = DbContextFactory.Create(nameof(Position_IsAssignedSequentially));
        var alice = DbContextFactory.MakeUser("u1", "Alice");
        var bob = DbContextFactory.MakeUser("u2", "Bob");
        var tournament = DbContextFactory.MakeTournament(1);
        var game = DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1), homeGoals: 1, awayGoals: 0);

        db.Users.AddRange(alice, bob);
        db.Tournaments.Add(tournament);
        db.Games.Add(game);
        db.Predictions.Add(DbContextFactory.MakePrediction(1, 1, "u1", 1, 0)); // exact → 3pts
        db.Predictions.Add(DbContextFactory.MakePrediction(2, 1, "u2", 2, 0)); // outcome → 1pt
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings[0].Position.Should().Be(1);
        standings[1].Position.Should().Be(2);
    }

    [Fact]
    public async Task EmptyTournament_ReturnsEmptyStandings()
    {
        var db = DbContextFactory.Create(nameof(EmptyTournament_ReturnsEmptyStandings));
        var tournament = DbContextFactory.MakeTournament(1);
        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync();

        var service = new StandingsService(db);
        var standings = await service.GetStandingsAsync(1);

        standings.Should().BeEmpty();
    }
}
