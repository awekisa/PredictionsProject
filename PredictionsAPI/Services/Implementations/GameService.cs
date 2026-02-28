using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Games;
using PredictionsAPI.Entities;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Services.Implementations;

public class GameService : IGameService
{
    private readonly AppDbContext _context;

    public GameService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GameResponse>> GetByTournamentAsync(int tournamentId)
    {
        return await _context.Games
            .Where(g => g.TournamentId == tournamentId)
            .OrderByDescending(g => g.StartTime)
            .Select(g => MapToResponse(g))
            .ToListAsync();
    }

    public async Task<GameResponse?> GetByIdAsync(int tournamentId, int gameId)
    {
        var game = await _context.Games
            .FirstOrDefaultAsync(g => g.Id == gameId && g.TournamentId == tournamentId);

        return game is null ? null : MapToResponse(game);
    }

    public async Task<GameResponse?> CreateAsync(int tournamentId, CreateGameRequest request)
    {
        var tournamentExists = await _context.Tournaments.AnyAsync(t => t.Id == tournamentId);
        if (!tournamentExists) return null;

        var game = new Game
        {
            TournamentId = tournamentId,
            HomeTeam = request.HomeTeam,
            AwayTeam = request.AwayTeam,
            StartTime = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc)
        };

        _context.Games.Add(game);
        await _context.SaveChangesAsync();

        return MapToResponse(game);
    }

    public async Task<GameResponse?> UpdateAsync(int tournamentId, int gameId, UpdateGameRequest request)
    {
        var game = await _context.Games
            .FirstOrDefaultAsync(g => g.Id == gameId && g.TournamentId == tournamentId);

        if (game is null) return null;

        game.HomeTeam = request.HomeTeam;
        game.AwayTeam = request.AwayTeam;
        game.StartTime = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc);

        await _context.SaveChangesAsync();

        return MapToResponse(game);
    }

    public async Task<bool> DeleteAsync(int tournamentId, int gameId)
    {
        var game = await _context.Games
            .FirstOrDefaultAsync(g => g.Id == gameId && g.TournamentId == tournamentId);

        if (game is null) return false;

        _context.Games.Remove(game);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<GameResponse?> SetResultAsync(int gameId, SetGameResultRequest request)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game is null) return null;

        if (DateTime.Now < game.StartTime)
            return null;

        game.HomeGoals = request.HomeGoals;
        game.AwayGoals = request.AwayGoals;
        game.IsFinished = true;

        await _context.SaveChangesAsync();

        return MapToResponse(game);
    }

    private static GameResponse MapToResponse(Game g) => new()
    {
        Id = g.Id,
        TournamentId = g.TournamentId,
        HomeTeam = g.HomeTeam,
        AwayTeam = g.AwayTeam,
        StartTime = g.StartTime,
        HomeGoals = g.HomeGoals,
        AwayGoals = g.AwayGoals,
        IsFinished = g.IsFinished
    };
}
