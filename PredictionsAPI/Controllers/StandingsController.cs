using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Authorize]
public class StandingsController : ControllerBase
{
    private readonly IStandingsService _standingsService;

    public StandingsController(IStandingsService standingsService)
    {
        _standingsService = standingsService;
    }

    [HttpGet("api/tournaments/{tournamentId}/standings")]
    public async Task<IActionResult> GetStandings(int tournamentId)
    {
        var standings = await _standingsService.GetStandingsAsync(tournamentId);
        return Ok(standings);
    }

    [HttpGet("api/tournaments/{tournamentId}/standings/{userDisplayName}/predictions")]
    public async Task<IActionResult> GetUserPredictionDetails(int tournamentId, string userDisplayName, [FromQuery] string type = "all")
    {
        var details = await _standingsService.GetUserPredictionDetailsAsync(tournamentId, userDisplayName, type);
        return Ok(details);
    }

    [HttpGet("api/standings/global")]
    public async Task<IActionResult> GetGlobalStandings()
    {
        var standings = await _standingsService.GetGlobalStandingsAsync();
        return Ok(standings);
    }

    [HttpGet("api/standings/global/{userDisplayName}/predictions")]
    public async Task<IActionResult> GetGlobalUserPredictionDetails(string userDisplayName, [FromQuery] string type = "all")
    {
        var details = await _standingsService.GetGlobalUserPredictionDetailsAsync(userDisplayName, type);
        return Ok(details);
    }
}
