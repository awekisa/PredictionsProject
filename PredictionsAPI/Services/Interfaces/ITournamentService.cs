using PredictionsAPI.DTOs.Tournaments;

namespace PredictionsAPI.Services.Interfaces;

public interface ITournamentService
{
    Task<List<TournamentResponse>> GetAllAsync();
    Task<TournamentResponse?> GetByIdAsync(int id);
    Task<TournamentResponse> CreateAsync(CreateTournamentRequest request);
    Task<TournamentResponse?> UpdateAsync(int id, UpdateTournamentRequest request);
    Task<bool> DeleteAsync(int id);
}
