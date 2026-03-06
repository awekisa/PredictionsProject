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
            .Where(g => g.TournamentId == tournamentId && g.IsFinished && g.HomeGoals.HasValue && g.AwayGoals.HasValue)
            .Select(g => new { g.Id, g.HomeGoals, g.AwayGoals })
            .ToListAsync();

        var finishedGameIds = gamesWithResults.Select(g => g.Id).ToHashSet();

        var allTournamentPredictions = await _context.Predictions
            .Include(p => p.User)
            .Where(p => p.Game.TournamentId == tournamentId)
            .ToListAsync();

        var gameResults = gamesWithResults.ToDictionary(g => g.Id);

        var userStats = allTournamentPredictions
            .GroupBy(p => new { p.UserId, p.User.DisplayName })
            .Select(group =>
            {
                int points = 0;
                int correctScores = 0;
                int correctOutcomes = 0;
                int totalPredictions = group.Count();

                foreach (var prediction in group)
                {
                    if (!finishedGameIds.Contains(prediction.GameId) ||
                        !gameResults.TryGetValue(prediction.GameId, out var result))
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
                        correctOutcomes++;
                    }
                }

                return new
                {
                    group.Key.DisplayName,
                    Points = points,
                    CorrectScores = correctScores,
                    CorrectOutcomes = correctOutcomes,
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
                CorrectOutcomes = userStats[i].CorrectOutcomes,
                TotalPredictions = userStats[i].TotalPredictions
            });
        }

        return standings;
    }

    public async Task<List<UserPredictionDetailResponse>> GetUserPredictionDetailsAsync(int tournamentId, string userDisplayName, string type)
    {
        var predictions = await _context.Predictions
            .Include(p => p.Game)
            .Include(p => p.User)
            .Where(p => p.Game.TournamentId == tournamentId && p.User.DisplayName == userDisplayName)
            .ToListAsync();

        var results = new List<UserPredictionDetailResponse>();

        foreach (var p in predictions)
        {
            var game = p.Game;
            bool isFinished = game.IsFinished && game.HomeGoals.HasValue && game.AwayGoals.HasValue;

            int pointsEarned = 0;
            int actualHome = 0;
            int actualAway = 0;

            if (isFinished)
            {
                actualHome = game.HomeGoals!.Value;
                actualAway = game.AwayGoals!.Value;

                if (IsCorrectScore(actualHome, actualAway, p.HomeGoals, p.AwayGoals))
                    pointsEarned = 3;
                else if (IsCorrectOutcome(actualHome, actualAway, p.HomeGoals, p.AwayGoals))
                    pointsEarned = 1;
            }

            bool include = type switch
            {
                "scores" => pointsEarned == 3,
                "outcomes" => pointsEarned == 1,
                "all" => pointsEarned > 0,
                "total" => true,
                _ => pointsEarned > 0
            };

            if (!include) continue;

            results.Add(new UserPredictionDetailResponse
            {
                HomeTeam = game.HomeTeam,
                AwayTeam = game.AwayTeam,
                HomeCrestUrl = game.HomeCrestUrl,
                AwayCrestUrl = game.AwayCrestUrl,
                PredictedHome = p.HomeGoals,
                PredictedAway = p.AwayGoals,
                ActualHome = actualHome,
                ActualAway = actualAway,
                PointsEarned = pointsEarned,
                MatchDate = game.StartTime
            });
        }

        return results.OrderByDescending(r => r.MatchDate).ToList();
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
