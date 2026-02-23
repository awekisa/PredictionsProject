using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Predictions;
using PredictionsAPI.Entities;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Services.Implementations;

public class PredictionService : IPredictionService
{
    private readonly AppDbContext _context;

    public PredictionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PredictionResponse?> PlacePredictionAsync(int gameId, string userId, PlacePredictionRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game is null) return null;

        if (DateTime.Now >= game.StartTime)
            return null;

        var existing = await _context.Predictions
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId);

        if (existing is not null)
        {
            existing.HomeGoals = request.HomeGoals;
            existing.AwayGoals = request.AwayGoals;
        }
        else
        {
            existing = new Prediction
            {
                GameId = gameId,
                UserId = userId,
                HomeGoals = request.HomeGoals,
                AwayGoals = request.AwayGoals,
                CreatedAt = DateTime.UtcNow
            };
            _context.Predictions.Add(existing);
        }

        await _context.SaveChangesAsync();

        return await BuildResponse(existing);
    }

    public async Task<List<PredictionResponse>> GetMyPredictionsAsync(int tournamentId, string userId)
    {
        return await _context.Predictions
            .Include(p => p.Game)
            .Include(p => p.User)
            .Where(p => p.Game.TournamentId == tournamentId && p.UserId == userId)
            .OrderByDescending(p => p.Game.StartTime)
            .Select(p => new PredictionResponse
            {
                Id = p.Id,
                GameId = p.GameId,
                HomeTeam = p.Game.HomeTeam,
                AwayTeam = p.Game.AwayTeam,
                HomeGoals = p.HomeGoals,
                AwayGoals = p.AwayGoals,
                UserDisplayName = p.User.DisplayName,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<List<PredictionResponse>> GetGamePredictionsAsync(int gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game is null) return new List<PredictionResponse>();

        if (DateTime.Now < game.StartTime)
            return new List<PredictionResponse>();

        return await _context.Predictions
            .Include(p => p.Game)
            .Include(p => p.User)
            .Where(p => p.GameId == gameId)
            .Select(p => new PredictionResponse
            {
                Id = p.Id,
                GameId = p.GameId,
                HomeTeam = p.Game.HomeTeam,
                AwayTeam = p.Game.AwayTeam,
                HomeGoals = p.HomeGoals,
                AwayGoals = p.AwayGoals,
                UserDisplayName = p.User.DisplayName,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();
    }

    private async Task<PredictionResponse> BuildResponse(Prediction prediction)
    {
        var game = await _context.Games.FindAsync(prediction.GameId);
        var user = await _context.Users.FindAsync(prediction.UserId);

        return new PredictionResponse
        {
            Id = prediction.Id,
            GameId = prediction.GameId,
            HomeTeam = game!.HomeTeam,
            AwayTeam = game.AwayTeam,
            HomeGoals = prediction.HomeGoals,
            AwayGoals = prediction.AwayGoals,
            UserDisplayName = ((ApplicationUser)user!).DisplayName,
            CreatedAt = prediction.CreatedAt
        };
    }
}
