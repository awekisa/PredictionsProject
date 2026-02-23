using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.DTOs.Tournaments;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/admin/tournaments")]
[Authorize(Roles = "Admin")]
public class AdminTournamentsController : ControllerBase
{
    private readonly ITournamentService _tournamentService;

    public AdminTournamentsController(ITournamentService tournamentService)
    {
        _tournamentService = tournamentService;
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTournamentRequest request)
    {
        var tournament = await _tournamentService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, tournament);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTournamentRequest request)
    {
        var tournament = await _tournamentService.UpdateAsync(id, request);
        if (tournament is null) return NotFound();
        return Ok(tournament);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _tournamentService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
