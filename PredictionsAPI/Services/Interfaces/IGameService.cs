using PredictionsAPI.DTOs.Games;

namespace PredictionsAPI.Services.Interfaces;

public interface IGameService
{
    Task<List<GameResponse>> GetByTournamentAsync(int tournamentId);
    Task<GameResponse?> GetByIdAsync(int tournamentId, int gameId);
    Task<GameResponse?> CreateAsync(int tournamentId, CreateGameRequest request);
    Task<GameResponse?> UpdateAsync(int tournamentId, int gameId, UpdateGameRequest request);
    Task<bool> DeleteAsync(int tournamentId, int gameId);
    Task<GameResponse?> SetResultAsync(int gameId, SetGameResultRequest request);
}
