using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/tournaments/{tournamentId}/games")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly IGameService _gameService;

    public GamesController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int tournamentId)
    {
        var games = await _gameService.GetByTournamentAsync(tournamentId);
        return Ok(games);
    }

    [HttpGet("{gameId}")]
    public async Task<IActionResult> GetById(int tournamentId, int gameId)
    {
        var game = await _gameService.GetByIdAsync(tournamentId, gameId);
        if (game is null) return NotFound();
        return Ok(game);
    }
}
