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
    private static readonly HashSet<string> FinishedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "FT", "AET", "PEN"
    };

    private readonly FootballApiClient _apiClient;
    private readonly AppDbContext _context;

    public FootballSyncService(FootballApiClient apiClient, AppDbContext context)
    {
        _apiClient = apiClient;
        _context = context;
    }

    public async Task<List<LeagueSearchResult>> SearchLeaguesAsync(string query)
    {
        var leagues = await _apiClient.SearchLeaguesAsync(query);

        return leagues.Select(l => new LeagueSearchResult
        {
            LeagueId = l.League.Id,
            Name = l.League.Name,
            Country = l.Country.Name,
            Type = l.League.Type,
            Logo = l.League.Logo,
            Seasons = l.Seasons
                .Select(s => s.Year)
                .OrderByDescending(y => y)
                .ToList()
        }).ToList();
    }

    public async Task<ImportLeagueResponse> ImportLeagueAsync(ImportLeagueRequest request)
    {
        var fixtures = await _apiClient.GetFixturesAsync(request.LeagueId, request.Season);

        var existingFixtureIdsList = await _context.Games
            .Where(g => g.ExternalFixtureId != null)
            .Select(g => g.ExternalFixtureId!.Value)
            .ToListAsync();
        var existingFixtureIds = existingFixtureIdsList.ToHashSet();

        var tournament = new Tournament
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            ExternalLeagueId = request.LeagueId,
            ExternalSeason = request.Season
        };

        _context.Tournaments.Add(tournament);

        int gamesImported = 0;
        foreach (var fixture in fixtures)
        {
            if (existingFixtureIds.Contains(fixture.Fixture.Id))
                continue;

            var game = new Game
            {
                Tournament = tournament,
                HomeTeam = fixture.Teams.Home.Name,
                AwayTeam = fixture.Teams.Away.Name,
                StartTime = fixture.Fixture.Date.ToUniversalTime(),
                ExternalFixtureId = fixture.Fixture.Id
            };

            _context.Games.Add(game);
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

        var fixtures = await _apiClient.GetFixturesAsync(
            tournament.ExternalLeagueId.Value,
            tournament.ExternalSeason.Value);

        var finishedFixtures = fixtures
            .Where(f => FinishedStatuses.Contains(f.Fixture.Status.Short))
            .ToDictionary(f => f.Fixture.Id);

        int updated = 0;

        foreach (var game in tournament.Games)
        {
            if (game.ExternalFixtureId is null)
                continue;

            if (!finishedFixtures.TryGetValue(game.ExternalFixtureId.Value, out var fixture))
                continue;

            var newHome = fixture.Goals.Home;
            var newAway = fixture.Goals.Away;

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
