using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/tournaments/{tournamentId}/standings")]
[Authorize]
public class StandingsController : ControllerBase
{
    private readonly IStandingsService _standingsService;

    public StandingsController(IStandingsService standingsService)
    {
        _standingsService = standingsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetStandings(int tournamentId)
    {
        var standings = await _standingsService.GetStandingsAsync(tournamentId);
        return Ok(standings);
    }
}
