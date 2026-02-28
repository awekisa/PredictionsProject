using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Tournaments;
using PredictionsAPI.Entities;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Services.Implementations;

public class TournamentService : ITournamentService
{
    private readonly AppDbContext _context;

    public TournamentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TournamentResponse>> GetAllAsync()
    {
        return await _context.Tournaments
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => MapToResponse(t))
            .ToListAsync();
    }

    public async Task<TournamentResponse?> GetByIdAsync(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        return tournament is null ? null : MapToResponse(tournament);
    }

    public async Task<TournamentResponse> CreateAsync(CreateTournamentRequest request)
    {
        var tournament = new Tournament
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();

        return MapToResponse(tournament);
    }

    public async Task<TournamentResponse?> UpdateAsync(int id, UpdateTournamentRequest request)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament is null) return null;

        tournament.Name = request.Name;
        await _context.SaveChangesAsync();

        return MapToResponse(tournament);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament is null) return false;

        _context.Tournaments.Remove(tournament);
        await _context.SaveChangesAsync();

        return true;
    }

    private static TournamentResponse MapToResponse(Tournament t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        CreatedAt = t.CreatedAt,
        ExternalLeagueId = t.ExternalLeagueId,
        ExternalSeason = t.ExternalSeason,
        EmblemUrl = t.EmblemUrl
    };
}
