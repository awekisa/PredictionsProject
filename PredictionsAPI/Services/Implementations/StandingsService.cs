using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Standings;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Services.Implementations;

public class StandingsService : IStandingsService
{
    private readonly AppDbContext _context;

    public StandingsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<StandingEntryResponse>> GetStandingsAsync(int tournamentId)
    {
        var gamesWithResults = await _context.Games
            .Where(g => g.TournamentId == tournamentId && g.HomeGoals.HasValue && g.AwayGoals.HasValue)
            .Select(g => new { g.Id, g.HomeGoals, g.AwayGoals })
            .ToListAsync();

        var gameIds = gamesWithResults.Select(g => g.Id).ToHashSet();

        var allTournamentPredictions = await _context.Predictions
            .Include(p => p.User)
            .Where(p => p.Game.TournamentId == tournamentId && gameIds.Contains(p.GameId))
            .ToListAsync();

        var gameResults = gamesWithResults.ToDictionary(g => g.Id);

        var userStats = allTournamentPredictions
            .GroupBy(p => new { p.UserId, p.User.DisplayName })
            .Select(group =>
            {
                int points = 0;
                int correctScores = 0;
                int totalPredictions = group.Count();

                foreach (var prediction in group)
                {
                    if (!gameResults.TryGetValue(prediction.GameId, out var result))
                        continue;

                    int resultHome = result.HomeGoals!.Value;
                    int resultAway = result.AwayGoals!.Value;

                    if (IsCorrectScore(resultHome, resultAway, prediction.HomeGoals, prediction.AwayGoals))
                    {
                        points += 3;
                        correctScores++;
                    }
                    else if (IsCorrectOutcome(resultHome, resultAway, prediction.HomeGoals, prediction.AwayGoals))
                    {
                        points += 1;
                    }
                }

                return new
                {
                    group.Key.DisplayName,
                    Points = points,
                    CorrectScores = correctScores,
                    TotalPredictions = totalPredictions
                };
            })
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.CorrectScores)
            .ThenBy(s => s.TotalPredictions)
            .ToList();

        var standings = new List<StandingEntryResponse>();
        for (int i = 0; i < userStats.Count; i++)
        {
            standings.Add(new StandingEntryResponse
            {
                Position = i + 1,
                UserDisplayName = userStats[i].DisplayName,
                Points = userStats[i].Points,
                CorrectScores = userStats[i].CorrectScores,
                TotalPredictions = userStats[i].TotalPredictions
            });
        }

        return standings;
    }

    private static bool IsCorrectScore(int resultHome, int resultAway, int predHome, int predAway)
    {
        return resultHome == predHome && resultAway == predAway;
    }

    private static bool IsCorrectOutcome(int resultHome, int resultAway, int predHome, int predAway)
    {
        bool isCorrectDraw = resultHome == resultAway && predHome == predAway;
        bool isCorrectHomeWin = resultHome > resultAway && predHome > predAway;
        bool isCorrectAwayWin = resultHome < resultAway && predHome < predAway;

        return isCorrectDraw || isCorrectHomeWin || isCorrectAwayWin;
    }
}
