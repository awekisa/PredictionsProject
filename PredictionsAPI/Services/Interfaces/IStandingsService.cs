using PredictionsAPI.DTOs.Standings;

namespace PredictionsAPI.Services.Interfaces;

public interface IStandingsService
{
    Task<List<StandingEntryResponse>> GetStandingsAsync(int tournamentId);
}
