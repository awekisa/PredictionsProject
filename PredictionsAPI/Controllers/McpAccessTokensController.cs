using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PredictionsAPI.DTOs.Auth;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Controllers;

[ApiController]
[Route("api/auth/me/agent-connections")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class McpAccessTokensController(IMcpAccessTokenService tokens) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var result = await tokens.ListAsync(userId, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMcpAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        try
        {
            var result = await tokens.CreateAsync(userId, request, cancellationToken);
            return result is null ? Unauthorized() : Ok(result);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(JwtBearerDefaults.AuthenticationScheme); }
    }

    [HttpDelete("{tokenId:guid}")]
    public async Task<IActionResult> Revoke(Guid tokenId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        return await tokens.RevokeAsync(userId, tokenId, cancellationToken) ? NoContent() : NotFound();
    }
}
