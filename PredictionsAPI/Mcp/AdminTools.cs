using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ModelContextProtocol.Server;
using PredictionsAPI.DTOs.Football;
using PredictionsAPI.DTOs.Games;
using PredictionsAPI.DTOs.Tournaments;
using PredictionsAPI.FootballApi;
using PredictionsAPI.Security;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Mcp;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class McpOperationAttribute(string scope, string? targetArgument = null) : Attribute
{
    public string Scope { get; } = scope;
    public string? TargetArgument { get; } = targetArgument;
    public bool Mutates => Scope is McpScopes.AdminWrite or McpScopes.PredictionsWrite;
}

public record McpDeletedTarget(string Kind, string Id, bool Deleted = true);
public record McpScoreSyncResult(int TournamentId, int Updated);
public record McpFootballStatus(int? RequestsLimit, int? RequestsRemaining);

public sealed class AdminTools(ITournamentService tournaments, IGameService games,
    AdminDeletionService deletion, IFootballSyncService football, FootballApiStatusStore status)
{
    [McpOperation(McpScopes.AdminRead)]
    [McpServerTool(Name = "admin_list_users", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: list accounts, emails, roles and prediction counts ordered by account ID. Requires admin:read and current Admin role. Pagination defaults to 50, maximum 100.")]
    public async Task<McpPage<AdminUserResponse>> ListUsers(int offset = 0, int limit = 50) =>
        PredictionTools.Page((await deletion.GetUsersAsync()).OrderBy(x => x.Id, StringComparer.Ordinal), offset, limit);

    [McpOperation(McpScopes.AdminRead)]
    [McpServerTool(Name = "admin_list_predictions", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: list all predictions, including private and admin predictions, ordered by prediction ID. Requires admin:read and current Admin role. Uses offset/limit pagination (50 default, 100 maximum).")]
    public async Task<McpPage<AdminPredictionResponse>> ListPredictions(int offset = 0, int limit = 50) =>
        PredictionTools.Page((await deletion.GetPredictionsAsync()).OrderBy(x => x.Id), offset, limit);

    [McpOperation(McpScopes.AdminRead)]
    [McpServerTool(Name = "admin_get_football_status", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: read the last known football provider request quota; null values mean unknown. Requires admin:read and current Admin role.")]
    public McpFootballStatus GetFootballStatus()
    {
        var (limit, remaining) = status.Get();
        return new(limit, remaining);
    }

    [McpOperation(McpScopes.AdminRead)]
    [McpServerTool(Name = "admin_list_football_leagues", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true, UseStructuredContent = true), Description("Admin: list available football provider competitions, ordered by league ID. Requires admin:read and current Admin role. Uses offset/limit pagination (50 default, 100 maximum).")]
    public async Task<McpPage<LeagueSearchResult>> ListFootballLeagues(int offset = 0, int limit = 50) =>
        PredictionTools.Page((await football.GetCompetitionsAsync()).OrderBy(x => x.LeagueId).ThenBy(x => x.Name, StringComparer.Ordinal), offset, limit);

    [McpOperation(McpScopes.AdminWrite)]
    [McpServerTool(Name = "admin_create_tournament", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true), Description("Admin: create a tournament. Repeated calls create separate tournaments; never retry automatically. Requires admin:write and current Admin role.")]
    public Task<TournamentResponse> CreateTournament(CreateTournamentRequest request) => tournaments.CreateAsync(Validate(request));

    [McpOperation(McpScopes.AdminWrite, "tournamentId")]
    [McpServerTool(Name = "admin_update_tournament", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: replace the name of a tournament by ID. Requires admin:write and current Admin role.")]
    public async Task<TournamentResponse> UpdateTournament(int tournamentId, UpdateTournamentRequest request) =>
        await tournaments.UpdateAsync(tournamentId, Validate(request)) ?? throw new ToolInputException("Tournament not found.");

    [McpOperation(McpScopes.AdminWrite, "tournamentId")]
    [McpServerTool(Name = "admin_delete_tournament", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: permanently delete a tournament and cascade deletion of ALL its games and predictions, changing standings. Requires admin:write and current Admin role.")]
    public async Task<McpDeletedTarget> DeleteTournament(int tournamentId) =>
        await tournaments.DeleteAsync(tournamentId) ? new("tournament", tournamentId.ToString()) : throw new ToolInputException("Tournament not found.");

    [McpOperation(McpScopes.AdminWrite, "tournamentId")]
    [McpServerTool(Name = "admin_create_game", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true), Description("Admin: create a game in the explicit tournament. startTime must include a UTC offset; it becomes the prediction deadline. Repeated calls create new games. Requires admin:write and current Admin role.")]
    public async Task<GameResponse> CreateGame(int tournamentId, CreateGameRequest request) =>
        await games.CreateAsync(tournamentId, Validate(request)) ?? throw new ToolInputException("Tournament not found.");

    [McpOperation(McpScopes.AdminWrite, "gameId")]
    [McpServerTool(Name = "admin_update_game", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: update teams/kickoff for the game in the explicit tournament. Moving kickoff later does not reopen the original prediction deadline; moving earlier tightens it. Requires admin:write and current Admin role.")]
    public async Task<GameResponse> UpdateGame(int tournamentId, int gameId, UpdateGameRequest request) =>
        await games.UpdateAsync(tournamentId, gameId, Validate(request)) ?? throw new ToolInputException("Game not found in this tournament.");

    [McpOperation(McpScopes.AdminWrite, "gameId")]
    [McpServerTool(Name = "admin_delete_game", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: permanently delete this tournament's game and cascade ALL its predictions, changing standings. Requires admin:write and current Admin role.")]
    public async Task<McpDeletedTarget> DeleteGame(int tournamentId, int gameId) =>
        await games.DeleteAsync(tournamentId, gameId) ? new("game", gameId.ToString()) : throw new ToolInputException("Game not found in this tournament.");

    [McpOperation(McpScopes.AdminWrite, "gameId")]
    [McpServerTool(Name = "admin_set_game_result", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: set a final score after kickoff, replacing any previous score and recalculating standings on the next read. Requires admin:write and current Admin role.")]
    public async Task<GameResponse> SetGameResult(int gameId, SetGameResultRequest request) =>
        await games.SetResultAsync(gameId, Validate(request)) ?? throw new ToolInputException("Game not found or has not started yet.");

    [McpOperation(McpScopes.AdminWrite, "gameId")]
    [McpServerTool(Name = "admin_sync_game_score", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: apply a supplied live/final score and FIFA status, overwriting current scores. Both goal values must be present together or null; final scores require both. This is the existing score-sync operation, not a provider fetch, and can change standings. Requires admin:write and current Admin role.")]
    public async Task<GameResponse> SyncGameScore(int gameId, SyncGameScoreRequest request) =>
        await games.SyncScoreAsync(gameId, Validate(request)) ?? throw new ToolInputException("Game not found or score payload is invalid.");

    [McpOperation(McpScopes.AdminWrite, "gameId")]
    [McpServerTool(Name = "admin_clear_game_result", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: clear the game's score and finished flag. Previously awarded standings points disappear on the next read. Requires admin:write and current Admin role.")]
    public async Task<GameResponse> ClearGameResult(int gameId) =>
        await games.ClearResultAsync(gameId) ?? throw new ToolInputException("Game not found.");

    [McpOperation(McpScopes.AdminWrite, "request.leagueId")]
    [McpServerTool(Name = "admin_import_league", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true, UseStructuredContent = true), Description("Admin: import a provider league/season into a NEW tournament and add previously unimported fixtures. Repetition may create another tournament; never retry automatically. Requires admin:write and current Admin role.")]
    public Task<ImportLeagueResponse> ImportLeague(ImportLeagueRequest request) => football.ImportLeagueAsync(Validate(request));

    [McpOperation(McpScopes.AdminWrite, "tournamentId")]
    [McpServerTool(Name = "admin_backfill_fixtures", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true), Description("Admin: fetch the tournament's provider fixtures, link matching existing games and add missing determined fixtures. Returns affected counts. Requires admin:write and current Admin role; never retry automatically.")]
    public async Task<BackfillFixturesResponse> BackfillFixtures(int tournamentId)
    {
        await RequireTournament(tournamentId);
        return await football.BackfillFixturesAsync(tournamentId);
    }

    [McpOperation(McpScopes.AdminWrite, "tournamentId")]
    [McpServerTool(Name = "admin_sync_tournament_scores", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true, UseStructuredContent = true), Description("Admin: fetch provider scores and overwrite matching tournament game results/status, changing standings. Returns updated game count. Requires admin:write and current Admin role; never retry automatically.")]
    public async Task<McpScoreSyncResult> SyncTournamentScores(int tournamentId)
    {
        await RequireTournament(tournamentId);
        return new(tournamentId, await football.SyncScoresAsync(tournamentId));
    }

    [McpOperation(McpScopes.AdminWrite, "userId")]
    [McpServerTool(Name = "admin_delete_user", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: permanently delete the account with this exact userId, cascading its predictions and agent connections. This changes standings; deleting your own account revokes your access. Requires admin:write and current Admin role.")]
    public async Task<McpDeletedTarget> DeleteUser(string userId) =>
        await deletion.DeleteUserAsync(userId) ? new("user", userId) : throw new ToolInputException("User not found.");

    [McpOperation(McpScopes.AdminWrite, "predictionId")]
    [McpServerTool(Name = "admin_delete_prediction", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Admin: permanently delete the prediction with this exact ID, changing its owner's standings. Requires admin:write and current Admin role.")]
    public async Task<McpDeletedTarget> DeletePrediction(int predictionId) =>
        await deletion.DeletePredictionAsync(predictionId) ? new("prediction", predictionId.ToString()) : throw new ToolInputException("Prediction not found.");

    private async Task RequireTournament(int id)
    {
        if (await tournaments.GetByIdAsync(id) is null) throw new ToolInputException("Tournament not found.");
    }

    // Service calls do not execute MVC's model validation. Carry over the same DTO attributes.
    private static T Validate<T>(T request) where T : class
    {
        if (request is null) throw new ToolInputException("request must be an object matching the tool schema.");
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new ToolInputException(string.Join(" ", errors.Select(e => e.ErrorMessage)));
        if (request is CreateGameRequest create && create.StartTime == default || request is UpdateGameRequest update && update.StartTime == default)
            throw new ToolInputException("startTime is required.");
        if (request is ImportLeagueRequest import && (import.LeagueId <= 0 || import.Season <= 0 || string.IsNullOrWhiteSpace(import.Name)))
            throw new ToolInputException("leagueId and season must be positive and name must not be empty.");
        return request;
    }
}
