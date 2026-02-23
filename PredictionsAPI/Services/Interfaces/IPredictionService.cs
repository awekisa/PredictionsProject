using PredictionsAPI.DTOs.Predictions;

namespace PredictionsAPI.Services.Interfaces;

public interface IPredictionService
{
    Task<PredictionResponse?> PlacePredictionAsync(int gameId, string userId, PlacePredictionRequest request);
    Task<List<PredictionResponse>> GetMyPredictionsAsync(int tournamentId, string userId);
    Task<List<PredictionResponse>> GetGamePredictionsAsync(int gameId);
}
