using System.ComponentModel.DataAnnotations;
using PredictionsAPI.Security;

namespace PredictionsAPI.DTOs.Auth;

public class CreateMcpAccessTokenRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    public int ExpirationDays { get; set; } = 30;
    [Required, MinLength(1)]
    public string[] Scopes { get; set; } = [McpScopes.AppRead];
}

public record McpAccessTokenResponse(
    Guid Id, string Name, string[] Scopes, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);

// Keep the credential out of generated ToString() output as well as list responses.
public class CreatedMcpAccessTokenResponse
{
    public required string Token { get; init; }
    public required McpAccessTokenResponse Connection { get; init; }
}

public record McpAccessTokenListResponse(
    IReadOnlyList<McpAccessTokenResponse> Tokens, bool CanGrantAdminScopes);
