using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/tournaments")]
[Authorize]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _tournamentService;
    private readonly IFootballSyncService _footballSyncService;

    public TournamentsController(ITournamentService tournamentService, IFootballSyncService footballSyncService)
    {
        _tournamentService = tournamentService;
        _footballSyncService = footballSyncService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tournaments = await _tournamentService.GetAllAsync();
        return Ok(tournaments);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tournament = await _tournamentService.GetByIdAsync(id);
        if (tournament is null) return NotFound();
        return Ok(tournament);
    }

    [HttpGet("{id}/football-standings")]
    public async Task<IActionResult> GetFootballStandings(int id)
    {
        var result = await _footballSyncService.GetCompetitionStandingsAsync(id);
        if (result is null) return NoContent();
        return Ok(result);
    }
}
