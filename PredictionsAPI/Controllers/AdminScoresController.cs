using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.DTOs.Games;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/admin/games")]
[Authorize(Roles = "Admin")]
public class AdminScoresController : ControllerBase
{
    private readonly IGameService _gameService;

    public AdminScoresController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpPut("{gameId}/result")]
    public async Task<IActionResult> SetResult(int gameId, [FromBody] SetGameResultRequest request)
    {
        var game = await _gameService.SetResultAsync(gameId, request);
        if (game is null) return BadRequest("Game not found or has not started yet.");
        return Ok(game);
    }
}
