using PredictionsAPI.DTOs.Football;

namespace PredictionsAPI.Services.Interfaces;

public interface IFootballSyncService
{
    Task<List<LeagueSearchResult>> GetCompetitionsAsync();
    Task<ImportLeagueResponse> ImportLeagueAsync(ImportLeagueRequest request);
    Task<int> SyncScoresAsync(int tournamentId);
    Task<CompetitionStandingsResponse?> GetCompetitionStandingsAsync(int tournamentId);
}
