using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.Entities;

namespace PredictionsAPI.Tests.Helpers;

public static class DbContextFactory
{
    public static AppDbContext Create(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static ApplicationUser MakeUser(string id, string displayName)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = $"{displayName.ToLower()}@test.com",
            NormalizedUserName = $"{displayName.ToUpper()}@TEST.COM",
            Email = $"{displayName.ToLower()}@test.com",
            NormalizedEmail = $"{displayName.ToUpper()}@TEST.COM",
            SecurityStamp = Guid.NewGuid().ToString(),
            DisplayName = displayName
        };
    }

    public static Tournament MakeTournament(int id, string name = "Test Cup")
    {
        return new Tournament { Id = id, Name = name, CreatedAt = DateTime.UtcNow };
    }

    public static Game MakeGame(int id, int tournamentId, DateTime startTime,
        int? homeGoals = null, int? awayGoals = null)
    {
        return new Game
        {
            Id = id,
            TournamentId = tournamentId,
            HomeTeam = "Home FC",
            AwayTeam = "Away FC",
            StartTime = startTime,
            HomeGoals = homeGoals,
            AwayGoals = awayGoals,
            IsFinished = homeGoals.HasValue && awayGoals.HasValue
        };
    }

    public static Prediction MakePrediction(int id, int gameId, string userId,
        int homeGoals, int awayGoals)
    {
        return new Prediction
        {
            Id = id,
            GameId = gameId,
            UserId = userId,
            HomeGoals = homeGoals,
            AwayGoals = awayGoals,
            CreatedAt = DateTime.UtcNow
        };
    }
}
