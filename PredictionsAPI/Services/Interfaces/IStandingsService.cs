using PredictionsAPI.DTOs.Standings;

namespace PredictionsAPI.Services.Interfaces;

public interface IStandingsService
{
    Task<List<StandingEntryResponse>> GetStandingsAsync(int tournamentId);
    Task<List<UserPredictionDetailResponse>> GetUserPredictionDetailsAsync(int tournamentId, string userDisplayName, string type);
    Task<List<StandingEntryResponse>> GetGlobalStandingsAsync();
    Task<List<UserPredictionDetailResponse>> GetGlobalUserPredictionDetailsAsync(string userDisplayName, string type);
}
