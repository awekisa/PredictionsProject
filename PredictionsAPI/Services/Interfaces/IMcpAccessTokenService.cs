using System.Security.Claims;
using PredictionsAPI.DTOs.Auth;

namespace PredictionsAPI.Services.Interfaces;

public interface IMcpAccessTokenService
{
    Task<McpAccessTokenListResponse?> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task<CreatedMcpAccessTokenResponse?> CreateAsync(string userId, CreateMcpAccessTokenRequest request, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string userId, Guid tokenId, CancellationToken cancellationToken = default);
    Task<ClaimsPrincipal?> AuthenticateAsync(string credential, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string scope, CancellationToken cancellationToken = default);
}
