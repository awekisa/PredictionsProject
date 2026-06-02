using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Entities;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class AdminDeletionServiceTests
{
    [Fact]
    public async Task DeleteTournament_RemovesRelatedGamesAndPredictions()
    {
        var db = DbContextFactory.Create(nameof(DeleteTournament_RemovesRelatedGamesAndPredictions));
        db.Users.Add(DbContextFactory.MakeUser("u1", "Alice"));
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        db.Games.AddRange(
            DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1)),
            DbContextFactory.MakeGame(2, 1, DateTime.UtcNow.AddDays(-1)));
        db.Predictions.AddRange(
            DbContextFactory.MakePrediction(1, 1, "u1", 2, 1),
            DbContextFactory.MakePrediction(2, 2, "u1", 1, 1));
        await db.SaveChangesAsync();

        var service = new TournamentService(db);
        var deleted = await service.DeleteAsync(1);

        deleted.Should().BeTrue();
        db.Tournaments.Should().BeEmpty();
        db.Games.Should().BeEmpty();
        db.Predictions.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletePrediction_RemovesOnlyThatPrediction()
    {
        var db = DbContextFactory.Create(nameof(DeletePrediction_RemovesOnlyThatPrediction));
        db.Users.AddRange(DbContextFactory.MakeUser("u1", "Alice"), DbContextFactory.MakeUser("u2", "Bob"));
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        db.Games.Add(DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1)));
        db.Predictions.AddRange(
            DbContextFactory.MakePrediction(1, 1, "u1", 2, 1),
            DbContextFactory.MakePrediction(2, 1, "u2", 0, 0));
        await db.SaveChangesAsync();

        var service = new AdminDeletionService(db);
        var deleted = await service.DeletePredictionAsync(1);

        deleted.Should().BeTrue();
        db.Predictions.Should().ContainSingle(p => p.Id == 2);
    }

    [Fact]
    public async Task DeleteUser_RemovesUserAndPredictions()
    {
        var db = DbContextFactory.Create(nameof(DeleteUser_RemovesUserAndPredictions));
        db.Users.AddRange(DbContextFactory.MakeUser("u1", "Alice"), DbContextFactory.MakeUser("u2", "Bob"));
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        db.Games.Add(DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddDays(-1)));
        db.Predictions.AddRange(
            DbContextFactory.MakePrediction(1, 1, "u1", 2, 1),
            DbContextFactory.MakePrediction(2, 1, "u2", 0, 0));
        await db.SaveChangesAsync();

        var service = new AdminDeletionService(db);
        var deleted = await service.DeleteUserAsync("u1");

        deleted.Should().BeTrue();
        db.Users.Should().ContainSingle(u => u.Id == "u2");
        db.Predictions.Should().ContainSingle(p => p.UserId == "u2");
    }
}
