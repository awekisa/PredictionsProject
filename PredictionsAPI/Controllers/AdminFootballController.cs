using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.DTOs.Football;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/admin/football")]
[Authorize(Roles = "Admin")]
public class AdminFootballController : ControllerBase
{
    private readonly IFootballSyncService _footballSyncService;

    public AdminFootballController(IFootballSyncService footballSyncService)
    {
        _footballSyncService = footballSyncService;
    }

    [HttpGet("leagues")]
    public async Task<IActionResult> SearchLeagues([FromQuery] string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return BadRequest("search query is required");

        var results = await _footballSyncService.SearchLeaguesAsync(search);
        return Ok(results);
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportLeague([FromBody] ImportLeagueRequest request)
    {
        var result = await _footballSyncService.ImportLeagueAsync(request);
        return Ok(result);
    }

    [HttpPost("tournaments/{id:int}/sync-scores")]
    public async Task<IActionResult> SyncScores(int id)
    {
        var updated = await _footballSyncService.SyncScoresAsync(id);
        return Ok(new { updated });
    }
}
