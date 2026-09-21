using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using ModelContextProtocol.Server;
using PredictionsAPI.DTOs.Football;
using PredictionsAPI.DTOs.Games;
using PredictionsAPI.DTOs.Predictions;
using PredictionsAPI.DTOs.Standings;
using PredictionsAPI.DTOs.Tournaments;
using PredictionsAPI.Security;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Mcp;

public record McpPage<T>(IReadOnlyList<T> Items, int Offset, int Limit, int Total, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? NextOffset);
public record McpCurrentUser(string AccountId, string DisplayName, string[] Roles, string[] Scopes);
public record McpFootballRow(string Stage, string? Group, StandingRowResponse Standing);

// Only this explicit tool class is registered. Services retain the website's business rules.
public sealed class PredictionTools(ITournamentService tournaments, IGameService games,
    IPredictionService predictions, IStandingsService standings, IFootballSyncService football, AppDbContext db)
{
    [McpServerTool(Name = "get_current_user", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read the delegated account identity, current roles and connection scopes. Never returns credentials.")]
    public McpCurrentUser GetCurrentUser(ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier)!, user.Identity!.Name!,
        user.FindAll(ClaimTypes.Role).Select(c => c.Value).Order().ToArray(),
        user.FindAll(McpAuthentication.ScopeClaim).Select(c => c.Value).Order().ToArray());

    [McpServerTool(Name = "list_tournaments", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("List tournaments ordered by ID. Lists use offset pagination: default limit 50, maximum 100; follow nextOffset until null.")]
    public async Task<McpPage<TournamentResponse>> ListTournaments(int offset = 0, int limit = 50) =>
        Page((await tournaments.GetAllAsync()).OrderBy(x => x.Id), offset, limit);

    [McpServerTool(Name = "get_tournament", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read a tournament by its positive tournamentId.")]
    public async Task<TournamentResponse> GetTournament(int tournamentId) =>
        await tournaments.GetByIdAsync(tournamentId) ?? throw new ToolInputException("Tournament not found.");

    [McpServerTool(Name = "list_games", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("List tournament fixtures ordered by game ID, including kickoff and original prediction deadline. Uses offset/limit pagination.")]
    public async Task<McpPage<GameResponse>> ListGames(int tournamentId, int offset = 0, int limit = 50)
    {
        await GetTournament(tournamentId);
        return Page((await games.GetByTournamentAsync(tournamentId)).OrderBy(x => x.Id), offset, limit);
    }

    [McpServerTool(Name = "get_game", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read a fixture using its tournamentId and gameId, including scores and prediction deadline.")]
    public async Task<GameResponse> GetGame(int tournamentId, int gameId) =>
        await games.GetByIdAsync(tournamentId, gameId) ?? throw new ToolInputException("Game not found in this tournament.");

    [McpServerTool(Name = "get_my_predictions", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read only the delegated account's predictions in a tournament, ordered by prediction ID. Uses offset/limit pagination.")]
    public async Task<McpPage<PredictionResponse>> GetMyPredictions(ClaimsPrincipal user, int tournamentId, int offset = 0, int limit = 50)
    {
        await GetTournament(tournamentId);
        return Page((await predictions.GetMyPredictionsAsync(tournamentId, user.FindFirstValue(ClaimTypes.NameIdentifier)!)).OrderBy(x => x.Id), offset, limit);
    }

    [McpServerTool(Name = "get_game_predictions", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read public predictions for a game ordered by prediction ID. Returns an empty page before kickoff; admin predictions stay hidden. Uses offset/limit pagination.")]
    public async Task<McpPage<PredictionResponse>> GetGamePredictions(int gameId, int offset = 0, int limit = 50)
    {
        if (!await db.Games.AnyAsync(g => g.Id == gameId)) throw new ToolInputException("Game not found.");
        return Page((await predictions.GetGamePredictionsAsync(gameId)).OrderBy(x => x.Id), offset, limit);
    }

    [McpServerTool(Name = "get_tournament_standings", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read tournament prediction standings ordered by display name for stable pagination; Position retains the website rank. Uses offset/limit pagination.")]
    public async Task<McpPage<StandingEntryResponse>> GetTournamentStandings(int tournamentId, int offset = 0, int limit = 50)
    {
        await GetTournament(tournamentId);
        return Page(StableStandings(await standings.GetStandingsAsync(tournamentId)), offset, limit);
    }

    [McpServerTool(Name = "get_global_standings", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read global prediction standings ordered by display name for stable pagination; Position retains the website rank. Uses offset/limit pagination.")]
    public async Task<McpPage<StandingEntryResponse>> GetGlobalStandings(int offset = 0, int limit = 50) =>
        Page(StableStandings(await standings.GetGlobalStandingsAsync()), offset, limit);

    [McpServerTool(Name = "get_user_prediction_details", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Read public prediction details for a display name, globally or within optional tournamentId. type: all (earned points), scores (exact), outcomes (correct result), total (all visible). Sorted by match date then full detail fields. Uses offset/limit pagination.")]
    public async Task<McpPage<UserPredictionDetailResponse>> GetUserPredictionDetails(string userDisplayName, int? tournamentId = null, string type = "all", int offset = 0, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(userDisplayName) || userDisplayName.Length > 256)
            throw new ToolInputException("userDisplayName must contain 1 to 256 characters.");
        if (type is not ("all" or "scores" or "outcomes" or "total"))
            throw new ToolInputException("type must be all, scores, outcomes or total.");
        if (tournamentId is not null) await GetTournament(tournamentId.Value);
        var result = tournamentId is not null
            ? await standings.GetUserPredictionDetailsAsync(tournamentId.Value, userDisplayName, type)
            : await standings.GetGlobalUserPredictionDetailsAsync(userDisplayName, type);
        return Page(result.OrderByDescending(x => x.MatchDate).ThenBy(x => JsonSerializer.Serialize(x), StringComparer.Ordinal), offset, limit);
    }

    [McpServerTool(Name = "get_football_standings", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true, UseStructuredContent = true), Description("Read official football standings for a tournament. Returns flattened stage/group/team rows ordered by stage, group and position, or an empty page when unavailable. Uses offset/limit pagination.")]
    public async Task<McpPage<McpFootballRow>> GetFootballStandings(int tournamentId, int offset = 0, int limit = 50)
    {
        await GetTournament(tournamentId);
        var result = await football.GetCompetitionStandingsAsync(tournamentId);
        var rows = result?.Groups.SelectMany(g => g.Table.Select(r => new McpFootballRow(g.Stage, g.Group, r))) ?? [];
        return Page(rows.OrderBy(x => x.Stage, StringComparer.Ordinal).ThenBy(x => x.Group, StringComparer.Ordinal)
            .ThenBy(x => x.Standing.Position).ThenBy(x => JsonSerializer.Serialize(x.Standing), StringComparer.Ordinal), offset, limit);
    }

    [McpServerTool(Name = "save_my_prediction", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Save or overwrite the delegated account's prediction before its original deadline. gameId must be positive; homeGoals and awayGoals must be nonnegative integers. Repeating updates the same record. The server never retries mutations.")]
    public async Task<PredictionResponse> SaveMyPrediction(ClaimsPrincipal user, int gameId, int homeGoals, int awayGoals) =>
        await predictions.PlacePredictionAsync(gameId, user.FindFirstValue(ClaimTypes.NameIdentifier)!,
            new PlacePredictionRequest { HomeGoals = homeGoals, AwayGoals = awayGoals })
        ?? throw new ToolInputException("Game not found or predictions are closed for this game.");

    private static IEnumerable<StandingEntryResponse> StableStandings(IEnumerable<StandingEntryResponse> rows) =>
        rows.OrderBy(x => x.UserDisplayName, StringComparer.Ordinal).ThenBy(x => x.Points).ThenBy(x => x.CorrectScores)
            .ThenBy(x => x.CorrectOutcomes).ThenBy(x => x.TotalPredictions).ThenBy(x => x.Position);

    private static McpPage<T> Page<T>(IEnumerable<T> source, int offset, int limit)
    {
        var rows = source.ToList();
        var items = rows.Skip(offset).Take(limit).ToList();
        return new(items, offset, limit, rows.Count, (long)offset + items.Count < rows.Count ? offset + items.Count : null);
    }
}

internal sealed class ToolInputException(string message) : Exception(message);
