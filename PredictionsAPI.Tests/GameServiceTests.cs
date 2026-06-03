using FluentAssertions;
using PredictionsAPI.DTOs.Games;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Tests.Helpers;

namespace PredictionsAPI.Tests;

public class GameServiceTests
{
    [Fact]
    public async Task CreateAsync_ConvertsOffsetStartTimeToUtcInstant()
    {
        var db = DbContextFactory.Create(nameof(CreateAsync_ConvertsOffsetStartTimeToUtcInstant));
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        await db.SaveChangesAsync();

        var service = new GameService(db);
        var result = await service.CreateAsync(1, new CreateGameRequest
        {
            HomeTeam = "Bulgaria",
            AwayTeam = "Spain",
            StartTime = new DateTimeOffset(2026, 6, 11, 22, 0, 0, TimeSpan.FromHours(3))
        });

        result.Should().NotBeNull();
        result!.StartTime.Kind.Should().Be(DateTimeKind.Utc);
        result.StartTime.Should().Be(new DateTime(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc));
        db.Games.Single().StartTime.Should().Be(result.StartTime);
    }

    [Fact]
    public async Task UpdateAsync_ConvertsOffsetStartTimeToUtcInstant()
    {
        var db = DbContextFactory.Create(nameof(UpdateAsync_ConvertsOffsetStartTimeToUtcInstant));
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        db.Games.Add(DbContextFactory.MakeGame(1, 1, new DateTime(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var service = new GameService(db);
        var result = await service.UpdateAsync(1, 1, new UpdateGameRequest
        {
            HomeTeam = "Bulgaria",
            AwayTeam = "Spain",
            StartTime = new DateTimeOffset(2026, 6, 12, 22, 0, 0, TimeSpan.FromHours(3))
        });

        result.Should().NotBeNull();
        result!.StartTime.Kind.Should().Be(DateTimeKind.Utc);
        result.StartTime.Should().Be(new DateTime(2026, 6, 12, 19, 0, 0, DateTimeKind.Utc));
        db.Games.Single().StartTime.Should().Be(result.StartTime);
    }

    [Fact]
    public async Task SetResultAsync_UsesUtcDeadlineComparison()
    {
        var db = DbContextFactory.Create(nameof(SetResultAsync_UsesUtcDeadlineComparison));
        db.Tournaments.Add(DbContextFactory.MakeTournament(1));
        db.Games.Add(DbContextFactory.MakeGame(1, 1, DateTime.UtcNow.AddHours(-1)));
        await db.SaveChangesAsync();

        var service = new GameService(db);
        var result = await service.SetResultAsync(1, new SetGameResultRequest
        {
            HomeGoals = 2,
            AwayGoals = 1
        });

        result.Should().NotBeNull();
        result!.IsFinished.Should().BeTrue();
        result.HomeGoals.Should().Be(2);
        result.AwayGoals.Should().Be(1);
    }
}
