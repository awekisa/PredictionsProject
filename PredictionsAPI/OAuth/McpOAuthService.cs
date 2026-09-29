using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PredictionsAPI.Data;
using PredictionsAPI.Entities;
using PredictionsAPI.Security;

namespace PredictionsAPI.OAuth;

public sealed class McpOAuthService(AppDbContext db, TimeProvider clock, IOptions<McpOAuthOptions> settings,
    IClientMetadataResolver metadata)
{
    private McpOAuthOptions Options => settings.Value;
    public static string Secret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool IsChallenge(string value) => Regex.IsMatch(value, "^[A-Za-z0-9_-]{43}$");

    private McpOAuthClient ValidateClient(ClientMetadata input, string id, bool document = false)
    {
        if (string.IsNullOrWhiteSpace(input.ClientName) || input.ClientName.Length > 100 ||
            input.RedirectUris is not { Length: > 0 and <= 10 } ||
            input.RedirectUris.Any(x => !Options.AllowedRedirectUris.Contains(x, StringComparer.Ordinal)) ||
            (document ? input.AuthMethod is not (null or "none") : input.AuthMethod is not (null or "none" or "client_secret_post" or "client_secret_basic")) ||
            (document ? input.GrantTypes is not null && !input.GrantTypes.Contains("authorization_code") :
                input.GrantTypes?.Any(x => x is not ("authorization_code" or "refresh_token")) == true) ||
            input.ResponseTypes?.Any(x => x != "code") == true)
            throw new OAuthException("invalid_client_metadata");
        return new() { Id = id, Name = input.ClientName.Trim(), AuthMethod = input.AuthMethod ?? (document ? "none" : "client_secret_basic"), RedirectUris = input.RedirectUris.Distinct().ToArray(), CreatedAt = clock.GetUtcNow() };
    }

    public async Task<RegisteredClient> RegisterAsync(ClientMetadata input, CancellationToken ct)
    {
        var client = ValidateClient(input, Secret());
        var secret = client.AuthMethod == "none" ? null : Secret();
        client.SecretHash = secret is null ? null : Hash(secret);
        db.McpOAuthClients.Add(client);
        await db.SaveChangesAsync(ct);
        return new() { Client = client, Secret = secret };
    }

    public async Task<string> StartAsync(string clientId, string redirectUri, string resource, string responseType,
        string challenge, string method, string? scope, string? state, CancellationToken ct)
    {
        if (clientId.Length > 2048 || redirectUri.Length > 2048 || state?.Length > 2048 ||
            responseType != "code" || method != "S256" || !IsChallenge(challenge))
            throw new OAuthException("invalid_request");
        if (resource != Options.Resource) throw new OAuthException("invalid_target");
        var client = clientId.StartsWith("https://", StringComparison.Ordinal)
            ? ValidateClient(await metadata.ResolveAsync(clientId, ct), clientId, document: true)
            : await db.McpOAuthClients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId, ct);
        if (client is null) throw new OAuthException("invalid_client");
        if (!client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal) ||
            !Options.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal))
            throw new OAuthException("invalid_redirect_uri");
        var scopes = ParseScopes(scope ?? McpScopes.AppRead);
        var request = new McpOAuthRequest
        {
            Id = Secret(), ClientId = client.Id, ClientName = client.Name, RedirectUri = redirectUri,
            Resource = resource, Challenge = challenge, Scopes = scopes, State = state,
            ExpiresAt = clock.GetUtcNow().AddMinutes(10)
        };
        db.McpOAuthRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return Options.WebsiteOrigin + "/oauth/consent?request=" + request.Id;
    }

    public async Task<object> ConsentAsync(string id, string userId, CancellationToken ct)
    {
        var request = await PendingAsync(id, ct);
        if (!await UserExistsAsync(userId, ct)) throw new OAuthException("invalid_grant");
        var admin = await IsAdminAsync(userId, ct);
        return new { clientName = request.ClientName, clientId = request.ClientId, redirectUri = request.RedirectUri,
            scopes = request.Scopes.Where(x => admin || !McpScopes.IsAdmin(x)).ToArray() };
    }

    public async Task<string> DecideAsync(string id, string userId, string[] scopes, bool approve, CancellationToken ct)
    {
        var request = await PendingAsync(id, ct);
        if (!await UserExistsAsync(userId, ct)) throw new OAuthException("invalid_grant");
        string? code = null;
        if (approve)
        {
            if (scopes.Length == 0 || scopes.Any(x => !request.Scopes.Contains(x)) ||
                (scopes.Any(McpScopes.IsAdmin) && !await IsAdminAsync(userId, ct)))
                throw new OAuthException("invalid_scope");
            code = Secret();
            request.UserId = userId;
            request.Scopes = scopes.Distinct().Order().ToArray();
            request.CodeHash = Hash(code);
            request.ExpiresAt = clock.GetUtcNow().AddMinutes(2);
        }
        request.Decided = true;
        request.Version = Guid.NewGuid();
        await SaveOnceAsync(ct);
        return QueryHelpers.AddQueryString(request.RedirectUri, new Dictionary<string, string?>
        {
            [approve ? "code" : "error"] = code ?? "access_denied", ["state"] = request.State, ["iss"] = Options.Issuer
        });
    }

    public async Task<object> ExchangeAsync(string grantType, string clientId, string resource, string? code,
        string? redirectUri, string? verifier, string? refreshToken, string? scope, CancellationToken ct, string? clientSecret = null, string authMethod = "none")
    {
        if (resource != Options.Resource) throw new OAuthException("invalid_target");
        if (string.IsNullOrEmpty(clientId) || clientId.Length > 2048) throw new OAuthException("invalid_client");
        if (!clientId.StartsWith("https://", StringComparison.Ordinal))
        {
            var client = await db.McpOAuthClients.AsNoTracking().SingleOrDefaultAsync(x => x.Id == clientId, ct);
            if (client is null || client.AuthMethod != authMethod || (client.AuthMethod != "none" &&
                (clientSecret is null || clientSecret.Length > 256 || client.SecretHash is null ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(client.SecretHash), Encoding.ASCII.GetBytes(Hash(clientSecret))))))
                throw new OAuthException("invalid_client");
        }
        else if (authMethod != "none") throw new OAuthException("invalid_client");
        // Each SaveChanges is one database transaction. Concurrency tokens on consumed credentials and
        // the connection ensure that a racing exchange/revocation cannot commit a second usable token.
        McpAccessToken connection;
        if (grantType == "authorization_code")
        {
            if (code is null || code.Length != 43 || verifier is null ||
                !Regex.IsMatch(verifier, "^[A-Za-z0-9._~-]{43,128}$")) throw new OAuthException("invalid_grant");
            var hash = Hash(code);
            var request = await db.McpOAuthRequests.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
            if (request is null || !request.Decided || request.Consumed || request.ExpiresAt <= clock.GetUtcNow() ||
                request.ClientId != clientId || request.RedirectUri != redirectUri || request.Resource != resource ||
                request.UserId is null || !await UserExistsAsync(request.UserId, ct) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(request.Challenge),
                    Encoding.ASCII.GetBytes(WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))))))
                throw new OAuthException("invalid_grant");
            request.Consumed = true;
            request.Version = Guid.NewGuid();
            connection = new()
            {
                Id = Guid.NewGuid(), UserId = request.UserId, Name = request.ClientName,
                OAuthClientId = clientId, Resource = resource, Scopes = request.Scopes,
                CreatedAt = clock.GetUtcNow(), ExpiresAt = clock.GetUtcNow().AddDays(90)
            };
            db.McpAccessTokens.Add(connection);
        }
        else if (grantType == "refresh_token")
        {
            if (refreshToken is null || refreshToken.Length != 43) throw new OAuthException("invalid_grant");
            var hash = Hash(refreshToken);
            var refresh = await db.McpOAuthRefreshTokens.Include(x => x.Connection).SingleOrDefaultAsync(x => x.Hash == hash, ct);
            if (refresh is null || refresh.Connection.OAuthClientId != clientId || refresh.Connection.Resource != resource)
                throw new OAuthException("invalid_grant");
            connection = refresh.Connection;
            if (refresh.Consumed)
            {
                // Retain used hashes until the connection expires: replay revokes the whole token family.
                if (db.Database.IsRelational())
                    await db.McpAccessTokens.Where(x => x.Id == connection.Id).ExecuteUpdateAsync(
                        s => s.SetProperty(x => x.RevokedAt, clock.GetUtcNow()).SetProperty(x => x.Version, Guid.NewGuid()), ct);
                else { connection.RevokedAt = clock.GetUtcNow(); connection.Version = Guid.NewGuid(); await SaveOnceAsync(ct); }
                throw new OAuthException("invalid_grant");
            }
            if (connection.RevokedAt is not null || connection.ExpiresAt <= clock.GetUtcNow() ||
                !await UserExistsAsync(connection.UserId, ct)) throw new OAuthException("invalid_grant");
            if (scope is not null)
            {
                var requested = ParseScopes(scope);
                if (requested.Any(x => !connection.Scopes.Contains(x))) throw new OAuthException("invalid_scope");
                connection.Scopes = requested;
            }
            refresh.Consumed = true;
            refresh.Version = Guid.NewGuid();
        }
        else throw new OAuthException("unsupported_grant_type");
        var access = "pred_mcp_" + Secret();
        var refreshCredential = Secret();
        connection.TokenHash = Hash(access);
        connection.AccessTokenExpiresAt = clock.GetUtcNow().AddHours(1);
        connection.Version = Guid.NewGuid();
        db.McpOAuthRefreshTokens.Add(new() { Hash = Hash(refreshCredential), Connection = connection });
        await SaveOnceAsync(ct);
        return new { access_token = access, token_type = "Bearer", expires_in = 3600,
            refresh_token = refreshCredential, scope = string.Join(' ', connection.Scopes) };
    }

    private async Task<McpOAuthRequest> PendingAsync(string id, CancellationToken ct)
    {
        var r = await db.McpOAuthRequests.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || r.Decided || r.ExpiresAt <= clock.GetUtcNow()) throw new OAuthException("invalid_request");
        return r;
    }
    private async Task SaveOnceAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new OAuthException("invalid_grant"); }
    }
    private static string[] ParseScopes(string scope)
    {
        var result = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Order().ToArray();
        if (result.Length == 0 || result.Any(x => !McpScopes.All.Contains(x))) throw new OAuthException("invalid_scope");
        return result;
    }
    private Task<bool> UserExistsAsync(string id, CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(x => x.Id == id, ct);
    private Task<bool> IsAdminAsync(string id, CancellationToken ct) => db.UserRoles.Where(x => x.UserId == id)
        .Join(db.Roles, x => x.RoleId, r => r.Id, (_, r) => r.NormalizedName).AnyAsync(x => x == "ADMIN", ct);
}

public sealed class RegisteredClient
{
    public required McpOAuthClient Client { get; init; }
    public string? Secret { get; init; }
}
