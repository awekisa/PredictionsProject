using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<FootballSyncService> _logger;

    public FootballSyncService(FootballApiClient apiClient, AppDbContext context, ILogger<FootballSyncService> logger)
    {
        _apiClient = apiClient;
        _context = context;
        _logger = logger;
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
        _logger.LogInformation("Importing league {LeagueId} season {Season} as '{Name}'",
            request.LeagueId, request.Season, request.Name);

        var matches = await _apiClient.GetMatchesAsync(request.LeagueId, request.Season);

        var existingFixtureIds = (await _context.Games
            .Where(g => g.ExternalFixtureId != null)
            .Select(g => g.ExternalFixtureId!.Value)
            .ToListAsync()).ToHashSet();

        var competition = await _apiClient.GetCompetitionAsync(request.LeagueId);

        var tournament = new Tournament
        {
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            ExternalLeagueId = request.LeagueId,
            ExternalSeason = request.Season,
            EmblemUrl = competition?.Emblem
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
                ExternalFixtureId = match.Id,
                HomeCrestUrl = match.HomeTeam.Crest,
                AwayCrestUrl = match.AwayTeam.Crest,
                HomeTeamShort = match.HomeTeam.Tla,
                AwayTeamShort = match.AwayTeam.Tla
            });
            gamesImported++;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error saving imported league {LeagueId} ({GamesCount} games)",
                request.LeagueId, gamesImported);
            throw;
        }

        _logger.LogInformation("Imported league {LeagueId}: {GamesCount} games added to tournament {TournamentId}",
            request.LeagueId, gamesImported, tournament.Id);

        return new ImportLeagueResponse
        {
            Tournament = new TournamentResponse
            {
                Id = tournament.Id,
                Name = tournament.Name,
                CreatedAt = tournament.CreatedAt,
                ExternalLeagueId = tournament.ExternalLeagueId,
                ExternalSeason = tournament.ExternalSeason,
                EmblemUrl = tournament.EmblemUrl
            },
            GamesImported = gamesImported
        };
    }

    public async Task<BackfillFixturesResponse> BackfillFixturesAsync(int tournamentId)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Games)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament is null || tournament.ExternalLeagueId is null || tournament.ExternalSeason is null)
        {
            return new BackfillFixturesResponse();
        }

        var matches = await _apiClient.GetMatchesAsync(
            tournament.ExternalLeagueId.Value,
            tournament.ExternalSeason.Value);

        var result = new BackfillFixturesResponse
        {
            ProviderFixtures = matches.Count,
            ExistingGames = tournament.Games.Count
        };

        var gamesByFixtureId = tournament.Games
            .Where(g => g.ExternalFixtureId is not null)
            .ToDictionary(g => g.ExternalFixtureId!.Value);

        var unmatchedGames = tournament.Games
            .Where(g => g.ExternalFixtureId is null)
            .ToList();

        foreach (var match in matches)
        {
            var homeTeam = match.HomeTeam.ShortName ?? match.HomeTeam.Name;
            var awayTeam = match.AwayTeam.ShortName ?? match.AwayTeam.Name;

            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
            {
                result.SkippedUndetermined++;
                continue;
            }

            if (gamesByFixtureId.ContainsKey(match.Id))
            {
                result.SkippedExisting++;
                continue;
            }

            var matchingGame = unmatchedGames.FirstOrDefault(g => IsSameFixture(g, homeTeam, awayTeam, match.UtcDate));
            if (matchingGame is not null)
            {
                matchingGame.ExternalFixtureId = match.Id;
                matchingGame.HomeCrestUrl ??= match.HomeTeam.Crest;
                matchingGame.AwayCrestUrl ??= match.AwayTeam.Crest;
                matchingGame.HomeTeamShort ??= match.HomeTeam.Tla;
                matchingGame.AwayTeamShort ??= match.AwayTeam.Tla;
                gamesByFixtureId[match.Id] = matchingGame;
                unmatchedGames.Remove(matchingGame);
                result.MatchedExisting++;
                continue;
            }

            var newGame = new Game
            {
                TournamentId = tournament.Id,
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                StartTime = match.UtcDate,
                ExternalFixtureId = match.Id,
                HomeCrestUrl = match.HomeTeam.Crest,
                AwayCrestUrl = match.AwayTeam.Crest,
                HomeTeamShort = match.HomeTeam.Tla,
                AwayTeamShort = match.AwayTeam.Tla
            };
            _context.Games.Add(newGame);
            gamesByFixtureId[match.Id] = newGame;
            result.Added++;
        }

        if (result.Added > 0 || result.MatchedExisting > 0)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error saving fixture backfill for tournament {TournamentId}", tournamentId);
                throw;
            }
        }

        _logger.LogInformation(
            "Backfilled tournament {TournamentId}: provider={ProviderFixtures}, existing={ExistingGames}, added={Added}, matched={MatchedExisting}, skippedExisting={SkippedExisting}, skippedUndetermined={SkippedUndetermined}",
            tournamentId,
            result.ProviderFixtures,
            result.ExistingGames,
            result.Added,
            result.MatchedExisting,
            result.SkippedExisting,
            result.SkippedUndetermined);

        return result;
    }

    public async Task<CompetitionStandingsResponse?> GetCompetitionStandingsAsync(int tournamentId)
    {
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament?.ExternalLeagueId is null || tournament.ExternalSeason is null)
            return null;

        var standings = await _apiClient.GetStandingsAsync(
            tournament.ExternalLeagueId.Value,
            tournament.ExternalSeason.Value);

        if (standings.Count == 0)
            return null;

        return new CompetitionStandingsResponse
        {
            Groups = standings.Select(s => new StandingGroupResponse
            {
                Stage = s.Stage,
                Group = s.Group,
                Table = s.Table.Select(r => new StandingRowResponse
                {
                    Position = r.Position,
                    TeamName = r.Team.Name,
                    TeamCrest = r.Team.Crest,
                    PlayedGames = r.PlayedGames,
                    Won = r.Won,
                    Draw = r.Draw,
                    Lost = r.Lost,
                    GoalsFor = r.GoalsFor,
                    GoalsAgainst = r.GoalsAgainst,
                    GoalDifference = r.GoalDifference,
                    Points = r.Points
                }).ToList()
            }).ToList()
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

        var allMatches = matches.ToDictionary(m => m.Id);

        var activeMatches = matches
            .Where(m => m.Status is "FINISHED" or "IN_PLAY" or "PAUSED" or "HALFTIME")
            .ToDictionary(m => m.Id);

        int updated = 0;

        foreach (var game in tournament.Games)
        {
            if (game.ExternalFixtureId is null)
                continue;

            // Backfill TLA short names from any match (not just active ones)
            if ((game.HomeTeamShort is null || game.AwayTeamShort is null)
                && allMatches.TryGetValue(game.ExternalFixtureId.Value, out var anyMatch))
            {
                if (game.HomeTeamShort is null && anyMatch.HomeTeam.Tla is not null)
                {
                    game.HomeTeamShort = anyMatch.HomeTeam.Tla;
                    updated++;
                }
                if (game.AwayTeamShort is null && anyMatch.AwayTeam.Tla is not null)
                {
                    game.AwayTeamShort = anyMatch.AwayTeam.Tla;
                    updated++;
                }
            }

            if (!activeMatches.TryGetValue(game.ExternalFixtureId.Value, out var match))
                continue;

            // Use regularTime for knockout matches that went to extra time/penalties
            // so predictions are scored against the 90-minute result
            var scoreSource = match.Score.Duration is "EXTRA_TIME" or "PENALTY_SHOOTOUT"
                && match.Score.RegularTime is not null
                ? match.Score.RegularTime
                : match.Score.FullTime;

            var newHome = scoreSource.Home;
            var newAway = scoreSource.Away;
            var isFinished = match.Status == "FINISHED";

            if (game.IsFinished == isFinished && game.HomeGoals == newHome && game.AwayGoals == newAway)
                continue;

            game.HomeGoals = newHome;
            game.AwayGoals = newAway;
            game.IsFinished = isFinished;
            updated++;
        }

        if (updated > 0)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error saving score sync for tournament {TournamentId}", tournamentId);
                throw;
            }
        }

        return updated;
    }

    private static bool IsSameFixture(Game game, string homeTeam, string awayTeam, DateTime utcDate)
    {
        return NormalizeTeamName(game.HomeTeam) == NormalizeTeamName(homeTeam)
            && NormalizeTeamName(game.AwayTeam) == NormalizeTeamName(awayTeam)
            && game.StartTime.ToUniversalTime() == utcDate.ToUniversalTime();
    }

    private static string NormalizeTeamName(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
