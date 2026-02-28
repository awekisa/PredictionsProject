using PredictionsAPI.DTOs.Football;
using PredictionsAPI.DTOs.Tournaments;

namespace PredictionsAPI.Services.Interfaces;

public interface IFootballSyncService
{
    Task<List<LeagueSearchResult>> SearchLeaguesAsync(string query);
    Task<ImportLeagueResponse> ImportLeagueAsync(ImportLeagueRequest request);
    Task<int> SyncScoresAsync(int tournamentId);
}
