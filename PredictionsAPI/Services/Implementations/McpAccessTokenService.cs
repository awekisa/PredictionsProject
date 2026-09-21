using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.DTOs.Auth;
using PredictionsAPI.Entities;
using PredictionsAPI.Security;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Services.Implementations;

public class McpAccessTokenService(AppDbContext db, TimeProvider clock) : IMcpAccessTokenService
{
    private const string Prefix = "pred_mcp_";

    public async Task<McpAccessTokenListResponse?> ListAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken)) return null;
        var tokens = await db.McpAccessTokens.AsNoTracking().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).ToListAsync(cancellationToken);
        return new(tokens.Select(ToResponse).ToList(), await IsAdminAsync(userId, cancellationToken));
    }

    public async Task<CreatedMcpAccessTokenResponse?> CreateAsync(
        string userId, CreateMcpAccessTokenRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new ArgumentException("Connection name must contain 1 to 100 characters.");
        if (request.ExpirationDays is not (7 or 30 or 90))
            throw new ArgumentException("Choose an expiry of 7, 30 or 90 days.");
        if (request.Scopes is null || request.Scopes.Length == 0 || request.Scopes.Any(s => !McpScopes.All.Contains(s)))
            throw new ArgumentException("Select at least one supported permission.");
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken)) return null;
        if (request.Scopes.Any(McpScopes.IsAdmin) && !await IsAdminAsync(userId, cancellationToken))
            throw new UnauthorizedAccessException("Only administrators can grant admin permissions.");

        var credential = Prefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        var token = new McpAccessToken
        {
            Id = Guid.NewGuid(), UserId = userId, Name = name, TokenHash = Hash(credential),
            Scopes = request.Scopes.Distinct().Order().ToArray(), CreatedAt = now,
            ExpiresAt = now.AddDays(request.ExpirationDays)
        };
        db.McpAccessTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return new() { Token = credential, Connection = ToResponse(token) };
    }

    public async Task<bool> RevokeAsync(string userId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await db.McpAccessTokens.SingleOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);
        if (token is null || !await db.Users.AnyAsync(u => u.Id == userId, cancellationToken)) return false;
        token.RevokedAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ClaimsPrincipal?> AuthenticateAsync(string credential, CancellationToken cancellationToken = default)
    {
        if (credential.Length != Prefix.Length + 43 || !credential.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var hash = Hash(credential);
        var token = await db.McpAccessTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (!IsActive(token)) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == token!.UserId, cancellationToken);
        if (user is null) return null;
        var roles = await GetRolesAsync(user.Id, cancellationToken);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.DisplayName),
            new(McpAuthentication.TokenIdClaim, token!.Id.ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(token.Scopes.Select(s => new Claim(McpAuthentication.ScopeClaim, s)));

        // Update just this column: never write a stale RevokedAt over a concurrent revocation.
        if (db.Database.IsRelational())
            await db.McpAccessTokens.Where(t => t.Id == token.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, clock.GetUtcNow()), cancellationToken);
        else
        {
            var tracked = await db.McpAccessTokens.FindAsync([token.Id], cancellationToken);
            if (tracked is not null) { tracked.LastUsedAt = clock.GetUtcNow(); await db.SaveChangesAsync(cancellationToken); }
        }
        return new(new ClaimsIdentity(claims, McpAuthentication.Scheme));
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string scope, CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.AuthenticationType != McpAuthentication.Scheme || !McpScopes.All.Contains(scope) ||
            !Guid.TryParse(principal.FindFirstValue(McpAuthentication.TokenIdClaim), out var id)) return false;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        // Re-read for every policy evaluation, including when a client retains a session/principal.
        var token = await db.McpAccessTokens.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
        return IsActive(token) && token!.Scopes.Contains(scope) &&
            await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken) &&
            (!McpScopes.IsAdmin(scope) || await IsAdminAsync(userId!, cancellationToken));
    }

    private bool IsActive(McpAccessToken? token) => token is not null && token.RevokedAt is null && token.ExpiresAt > clock.GetUtcNow();
    private static string Hash(string credential) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));
    private Task<List<string>> GetRolesAsync(string userId, CancellationToken cancellationToken) =>
        db.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!).ToListAsync(cancellationToken);
    private Task<bool> IsAdminAsync(string userId, CancellationToken cancellationToken) =>
        db.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.NormalizedName)
            .AnyAsync(name => name == "ADMIN", cancellationToken);
    private static McpAccessTokenResponse ToResponse(McpAccessToken t) =>
        new(t.Id, t.Name, t.Scopes, t.CreatedAt, t.ExpiresAt, t.LastUsedAt, t.RevokedAt);
}
