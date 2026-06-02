using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.Services.Implementations;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminDeletionController : ControllerBase
{
    private readonly AdminDeletionService _service;

    public AdminDeletionController(AdminDeletionService service)
    {
        _service = service;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _service.GetUsersAsync();
        return Ok(users);
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var deleted = await _service.DeleteUserAsync(userId);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpGet("predictions")]
    public async Task<IActionResult> GetPredictions()
    {
        var predictions = await _service.GetPredictionsAsync();
        return Ok(predictions);
    }

    [HttpDelete("predictions/{predictionId:int}")]
    public async Task<IActionResult> DeletePrediction(int predictionId)
    {
        var deleted = await _service.DeletePredictionAsync(predictionId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
