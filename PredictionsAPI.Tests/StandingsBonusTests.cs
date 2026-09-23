using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Standings;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class StandingsBonusTests
{
    private static AppDbContext Seed(int outcomes, int exact = 0, int misses = 1)
    {
        var db = DbContextFactory.Create(Guid.NewGuid().ToString());
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        db.Users.Add(DbContextFactory.MakeUser("player", "Player"));
        AddPredictions(db, "player", outcomes, exact, misses);
        db.SaveChanges();
        return db;
    }

    private static void AddPredictions(AppDbContext db, string user, int outcomes, int exact = 0, int misses = 0)
    {
        int start = db.Games.Local.Select(g => g.Id).DefaultIfEmpty().Max() + 1;
        for (int i = 0; i < outcomes + exact + misses; i++)
        {
            int id = start + i;
            db.Games.Add(DbContextFactory.MakeGame(id, 1, DateTime.UtcNow.AddDays(-2), 2, 1));
            db.Predictions.Add(DbContextFactory.MakePrediction(id, id, user,
                i < outcomes ? 1 : i < outcomes + exact ? 2 : 0,
                i < outcomes ? 0 : i < outcomes + exact ? 1 : 1));
        }
    }

    private static async Task<List<StandingEntryResponse>[]> Both(StandingsService service) =>
        [await service.GetStandingsAsync(1), await service.GetGlobalStandingsAsync()];

    [Theory]
    [InlineData(0, 0, 6)]
    [InlineData(4, 0, 10)]
    [InlineData(5, 2, 13)]
    [InlineData(9, 2, 17)]
    [InlineData(10, 4, 20)]
    [InlineData(14, 4, 24)]
    [InlineData(15, 6, 27)]
    [InlineData(16, 6, 28)]
    public async Task CompleteSets_ExcludeExactScores_AndIncludeBonusOnce(int outcomes, int bonus, int points)
    {
        using var db = Seed(outcomes, exact: 2);
        var service = new StandingsService(db);
        for (int read = 0; read < 2; read++)
        foreach (var rows in await Both(service))
        {
            var row = rows.Should().ContainSingle().Subject;
            row.BonusPoints.Should().Be(bonus);
            row.Points.Should().Be(points);
            row.CorrectOutcomes.Should().Be(outcomes);
            row.CorrectScores.Should().Be(2);
            row.TotalPredictions.Should().Be(outcomes + 3);
            row.Position.Should().Be(1);
        }
        var details = await service.GetUserPredictionDetailsAsync(1, "Player", "total");
        details.Select(d => d.PointsEarned).Should().OnlyContain(p => p == 0 || p == 1 || p == 3);
        details.Sum(d => d.PointsEarned).Should().Be(outcomes + 6);
        (await service.GetGlobalUserPredictionDetailsAsync("Player", "total"))
            .Should().BeEquivalentTo(details);
    }

    [Fact]
    public async Task GlobalBonus_UsesCombinedCountsAcrossTournaments()
    {
        using var db = Seed(5, misses: 0);
        db.Tournaments.Add(DbContextFactory.MakeTournament(2));
        foreach (var game in db.Games.Where(g => g.Id > 3)) game.TournamentId = 2;
        await db.SaveChangesAsync();
        var service = new StandingsService(db);
        var first = (await service.GetStandingsAsync(1)).Single();
        var second = (await service.GetStandingsAsync(2)).Single();
        first.CorrectOutcomes.Should().Be(3);
        second.CorrectOutcomes.Should().Be(2);
        first.BonusPoints.Should().Be(0);
        second.BonusPoints.Should().Be(0);
        var global = (await service.GetGlobalStandingsAsync()).Single();
        global.CorrectOutcomes.Should().Be(5);
        global.BonusPoints.Should().Be(2);
        global.Points.Should().Be(7);
    }

    [Theory]
    [InlineData("correct")]
    [InlineData("clear")]
    [InlineData("unfinished")]
    [InlineData("missingHome")]
    [InlineData("missingAway")]
    [InlineData("deletePrediction")]
    [InlineData("deleteGame")]
    public async Task ChangedResultsOrDeletedData_RemoveBonusAndReorder(string change)
    {
        using var db = Seed(5, misses: 0);
        db.Users.Add(DbContextFactory.MakeUser("other", "Other"));
        AddPredictions(db, "other", 0, exact: 2);
        await db.SaveChangesAsync();
        var service = new StandingsService(db);
        foreach (var rows in await Both(service))
        {
            rows[0].UserDisplayName.Should().Be("Player");
            rows[0].Points.Should().Be(7);
            rows[0].BonusPoints.Should().Be(2);
        }
        var game = db.Games.Single(g => g.Id == 5);
        switch (change)
        {
            case "correct": game.HomeGoals = 0; game.AwayGoals = 1; break;
            case "clear": game.HomeGoals = null; game.AwayGoals = null; game.IsFinished = false; break;
            case "unfinished": game.IsFinished = false; break;
            case "missingHome": game.HomeGoals = null; break;
            case "missingAway": game.AwayGoals = null; break;
            case "deletePrediction": db.Predictions.Remove(db.Predictions.Single(p => p.GameId == 5)); break;
            case "deleteGame": db.Games.Remove(game); break;
        }
        await db.SaveChangesAsync();
        foreach (var rows in await Both(service))
        {
            rows.Select(r => r.UserDisplayName).Should().Equal("Other", "Player");
            rows.Select(r => r.Position).Should().Equal(1, 2);
            rows[1].CorrectOutcomes.Should().Be(4);
            rows[1].BonusPoints.Should().Be(0);
            rows[1].Points.Should().Be(4);
        }
    }

    [Fact]
    public async Task SettledResults_AwardBonusWithoutRequiringConsecutiveOutcomes()
    {
        using var db = Seed(6, misses: 0);
        // A miss between correct outcomes breaks the streak, but not the set.
        db.Games.Single(g => g.Id == 3).HomeGoals = 0;
        var last = db.Games.Single(g => g.Id == 6);
        last.IsFinished = false;
        await db.SaveChangesAsync();
        var service = new StandingsService(db);
        foreach (var rows in await Both(service)) rows.Single().BonusPoints.Should().Be(0);
        last.IsFinished = true;
        await db.SaveChangesAsync();
        foreach (var rows in await Both(service))
        {
            rows.Single().CorrectOutcomes.Should().Be(5);
            rows.Single().BonusPoints.Should().Be(2);
            rows.Single().Points.Should().Be(7);
        }
    }

    [Fact]
    public async Task AdjustedTies_KeepExactScoresThenFewerPredictions_AndExcludeAdmins()
    {
        using var db = Seed(5, misses: 0); // 7 points, no exact scores
        foreach (var user in new[] { "many", "few", "admin", "noPredictions" })
            db.Users.Add(DbContextFactory.MakeUser(user, user));
        AddPredictions(db, "many", 1, exact: 2, misses: 3); // 7 points, 6 predictions
        AddPredictions(db, "few", 1, exact: 2); // 7 points, 3 predictions
        AddPredictions(db, "admin", 15);
        db.Roles.Add(new IdentityRole { Id = "admin-role", Name = "Admin", NormalizedName = "ADMIN" });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "admin", RoleId = "admin-role" });
        await db.SaveChangesAsync();
        foreach (var rows in await Both(new StandingsService(db)))
        {
            rows.Select(r => r.UserDisplayName).Should().Equal("few", "many", "Player");
            rows.Select(r => r.Position).Should().Equal(1, 2, 3);
            rows.Select(r => r.Points).Should().Equal(7, 7, 7);
            rows.Select(r => r.BonusPoints).Should().Equal(0, 0, 2);
            rows.Select(r => r.TotalPredictions).Should().Equal(3, 6, 5);
        }
    }
}
