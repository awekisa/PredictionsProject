using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Football;
using PredictionsAPI.DTOs.Tournaments;
using PredictionsAPI.Entities;
using PredictionsAPI.FootballApi;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Services.Implementations;

public class FootballSyncService : IFootballSyncService
{
    private readonly FootballApiClient _apiClient;
    private readonly AppDbContext _context;

    public FootballSyncService(FootballApiClient apiClient, AppDbContext context)
    {
        _apiClient = apiClient;
        _context = context;
    }

    public async Task<List<LeagueSearchResult>> GetCompetitionsAsync()
    {
        var competitions = await _apiClient.GetCompetitionsAsync();

        return competitions.Select(c =>
        {
            var currentYear = c.CurrentSeason?.StartDate is { Length: >= 4 } s
                ? int.Parse(s[..4])
                : DateTime.UtcNow.Year;

            return new LeagueSearchResult
            {
                LeagueId = c.Id,
                Name = c.Name,
                Country = c.Area.Name,
                Type = c.Type,
                Logo = c.Emblem,
                Seasons = Enumerable.Range(0, 4).Select(i => currentYear - i).ToList()
            };
        }).ToList();
    }

    public async Task<ImportLeagueResponse> ImportLeagueAsync(ImportLeagueRequest request)
    {
        var matches = await _apiClient.GetMatchesAsync(request.LeagueId, request.Season);

        var existingFixtureIds = (await _context.Games
            .Where(g => g.ExternalFixtureId != null)
            .Select(g => g.ExternalFixtureId!.Value)
            .ToListAsync()).ToHashSet();

        var tournament = new Tournament
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            ExternalLeagueId = request.LeagueId,
            ExternalSeason = request.Season
        };

        _context.Tournaments.Add(tournament);

        int gamesImported = 0;
        foreach (var match in matches)
        {
            if (existingFixtureIds.Contains(match.Id))
                continue;

            var homeTeam = match.HomeTeam.ShortName ?? match.HomeTeam.Name;
            var awayTeam = match.AwayTeam.ShortName ?? match.AwayTeam.Name;

            // Skip fixtures where teams are not yet determined (e.g. cup knockout placeholders)
            if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam))
                continue;

            _context.Games.Add(new Game
            {
                Tournament = tournament,
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                StartTime = match.UtcDate,
                ExternalFixtureId = match.Id
            });
            gamesImported++;
        }

        await _context.SaveChangesAsync();

        return new ImportLeagueResponse
        {
            Tournament = new TournamentResponse
            {
                Id = tournament.Id,
                Name = tournament.Name,
                CreatedAt = tournament.CreatedAt,
                ExternalLeagueId = tournament.ExternalLeagueId,
                ExternalSeason = tournament.ExternalSeason
            },
            GamesImported = gamesImported
        };
    }

    public async Task<int> SyncScoresAsync(int tournamentId)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Games)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament is null || tournament.ExternalLeagueId is null || tournament.ExternalSeason is null)
            return 0;

        var matches = await _apiClient.GetMatchesAsync(
            tournament.ExternalLeagueId.Value,
            tournament.ExternalSeason.Value);

        var finishedMatches = matches
            .Where(m => m.Status == "FINISHED")
            .ToDictionary(m => m.Id);

        int updated = 0;

        foreach (var game in tournament.Games)
        {
            if (game.ExternalFixtureId is null)
                continue;

            if (!finishedMatches.TryGetValue(game.ExternalFixtureId.Value, out var match))
                continue;

            var newHome = match.Score.FullTime.Home;
            var newAway = match.Score.FullTime.Away;

            if (game.IsFinished && game.HomeGoals == newHome && game.AwayGoals == newAway)
                continue;

            game.HomeGoals = newHome;
            game.AwayGoals = newAway;
            game.IsFinished = true;
            updated++;
        }

        if (updated > 0)
            await _context.SaveChangesAsync();

        return updated;
    }
}
