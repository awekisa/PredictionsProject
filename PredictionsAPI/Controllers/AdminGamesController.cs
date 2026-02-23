using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.DTOs.Games;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/admin/tournaments/{tournamentId}/games")]
[Authorize(Roles = "Admin")]
public class AdminGamesController : ControllerBase
{
    private readonly IGameService _gameService;

    public AdminGamesController(IGameService gameService)
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

    [HttpPost]
    public async Task<IActionResult> Create(int tournamentId, [FromBody] CreateGameRequest request)
    {
        var game = await _gameService.CreateAsync(tournamentId, request);
        if (game is null) return NotFound("Tournament not found.");
        return CreatedAtAction(nameof(GetById), new { tournamentId, gameId = game.Id }, game);
    }

    [HttpPut("{gameId}")]
    public async Task<IActionResult> Update(int tournamentId, int gameId, [FromBody] UpdateGameRequest request)
    {
        var game = await _gameService.UpdateAsync(tournamentId, gameId, request);
        if (game is null) return NotFound();
        return Ok(game);
    }

    [HttpDelete("{gameId}")]
    public async Task<IActionResult> Delete(int tournamentId, int gameId)
    {
        var deleted = await _gameService.DeleteAsync(tournamentId, gameId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
