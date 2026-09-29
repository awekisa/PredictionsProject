namespace PredictionsAPI.Entities;

public class McpOAuthClient
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string[] RedirectUris { get; set; } = [];
    public string AuthMethod { get; set; } = "none";
    public string? SecretHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// A short-lived, opaque consent request becomes a one-use authorization code.
public class McpOAuthRequest
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string Resource { get; set; } = "";
    public string? State { get; set; }
    public string Challenge { get; set; } = "";
    public string[] Scopes { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string? CodeHash { get; set; }
    public bool Decided { get; set; }
    public bool Consumed { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public class McpOAuthRefreshToken
{
    public string Hash { get; set; } = "";
    public Guid ConnectionId { get; set; }
    public McpAccessToken Connection { get; set; } = null!;
    public bool Consumed { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
