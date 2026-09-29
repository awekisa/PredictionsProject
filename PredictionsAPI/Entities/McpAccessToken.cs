using System.Text.Json.Serialization;

namespace PredictionsAPI.Entities;

public class McpAccessToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    [JsonIgnore]
    public string TokenHash { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public string? OAuthClientId { get; set; }
    public string? Resource { get; set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
