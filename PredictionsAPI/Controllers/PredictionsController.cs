using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.DTOs.Predictions;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictionService;

    public PredictionsController(IPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    [HttpPost("games/{gameId}/predictions")]
    public async Task<IActionResult> PlacePrediction(int gameId, [FromBody] PlacePredictionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var prediction = await _predictionService.PlacePredictionAsync(gameId, userId, request);

        if (prediction is null)
            return BadRequest("Game not found or predictions are closed for this game.");

        return Ok(prediction);
    }

    [HttpGet("tournaments/{tournamentId}/my-predictions")]
    public async Task<IActionResult> GetMyPredictions(int tournamentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var predictions = await _predictionService.GetMyPredictionsAsync(tournamentId, userId);
        return Ok(predictions);
    }

    [HttpGet("games/{gameId}/predictions")]
    public async Task<IActionResult> GetGamePredictions(int gameId)
    {
        var predictions = await _predictionService.GetGamePredictionsAsync(gameId);
        return Ok(predictions);
    }
}
