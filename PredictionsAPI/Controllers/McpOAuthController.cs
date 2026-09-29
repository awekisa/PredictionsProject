using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PredictionsAPI.OAuth;
using PredictionsAPI.Security;

namespace PredictionsAPI.Controllers;

[ApiController]
[EnableRateLimiting("mcp-oauth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class McpOAuthController(McpOAuthService oauth, IOptions<McpOAuthOptions> settings) : ControllerBase
{
    [HttpGet("/.well-known/oauth-authorization-server")]
    public IActionResult Metadata()
    {
        var issuer = settings.Value.Issuer;
        return Ok(new { issuer, authorization_endpoint = issuer + "/oauth/authorize",
            token_endpoint = issuer + "/oauth/token", registration_endpoint = issuer + "/oauth/register",
            response_types_supported = new[] { "code" }, grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" }, token_endpoint_auth_methods_supported = new[] { "none", "client_secret_post", "client_secret_basic" },
            scopes_supported = McpScopes.All, client_id_metadata_document_supported = true,
            authorization_response_iss_parameter_supported = true });
    }

    [HttpPost("/oauth/register")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> Register(ClientMetadata input, CancellationToken ct) => await Run(async () =>
    {
        var registration = await oauth.RegisterAsync(input, ct);
        var c = registration.Client;
        var response = new Dictionary<string, object> { ["client_id"] = c.Id, ["client_name"] = c.Name,
            ["redirect_uris"] = c.RedirectUris, ["client_id_issued_at"] = c.CreatedAt.ToUnixTimeSeconds(),
            ["token_endpoint_auth_method"] = c.AuthMethod, ["grant_types"] = new[] { "authorization_code", "refresh_token" },
            ["response_types"] = new[] { "code" } };
        if (registration.Secret is not null) { response["client_secret"] = registration.Secret; response["client_secret_expires_at"] = 0; }
        return StatusCode(201, response);
    });

    [HttpGet("/oauth/authorize")]
    public async Task<IActionResult> AuthorizeClient(CancellationToken ct) => await Run(async () =>
    {
        RejectDuplicates(Request.Query.Select(x => (x.Key, x.Value.Count)));
        string Get(string key) => Request.Query[key].ToString();
        // Errors stay on this origin: an invalid redirect URI must never receive an OAuth redirect.
        return Redirect(await oauth.StartAsync(Get("client_id"), Get("redirect_uri"), Get("resource"), Get("response_type"),
            Get("code_challenge"), Get("code_challenge_method"), Request.Query.ContainsKey("scope") ? Get("scope") : null,
            Request.Query.ContainsKey("state") ? Get("state") : null, ct));
    });

    [HttpPost("/oauth/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> Token(CancellationToken ct) => await Run(async () =>
    {
        var form = await Request.ReadFormAsync(ct);
        RejectDuplicates(form.Select(x => (x.Key, x.Value.Count)));
        string? Get(string key) => form.TryGetValue(key, out var value) ? value.ToString() : null;
        var clientId = Get("client_id") ?? "";
        var secret = Get("client_secret");
        var method = secret is null ? "none" : "client_secret_post";
        if (Request.Headers.ContainsKey("Authorization"))
        {
            if (secret is not null || !System.Net.Http.Headers.AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header) ||
                header.Scheme != "Basic" || header.Parameter is null) throw new OAuthException("invalid_client");
            try
            {
                var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter)).Split(':', 2);
                if (parts.Length != 2) throw new OAuthException("invalid_client");
                var basicId = WebUtility.UrlDecode(parts[0]);
                if (clientId.Length > 0 && clientId != basicId) throw new OAuthException("invalid_client");
                clientId = basicId;
                secret = WebUtility.UrlDecode(parts[1]);
                method = "client_secret_basic";
            }
            catch (FormatException) { throw new OAuthException("invalid_client"); }
        }
        return Ok(await oauth.ExchangeAsync(Get("grant_type") ?? "", clientId, Get("resource") ?? "",
            Get("code"), Get("redirect_uri"), Get("code_verifier"), Get("refresh_token"), Get("scope"), ct, secret, method));
    });

    [HttpGet("/api/auth/mcp-oauth/consent/{id}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Consent(string id, CancellationToken ct) => await Run(async () =>
        Ok(await oauth.ConsentAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct)));

    [HttpPost("/api/auth/mcp-oauth/consent/{id}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Consumes("application/json")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Decide(string id, ConsentDecision decision, CancellationToken ct) => await Run(async () =>
        Ok(new { redirectUrl = await oauth.DecideAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            decision.Scopes ?? [], decision.Approve, ct) }));

    private static void RejectDuplicates(IEnumerable<(string Key, int Count)> fields)
    {
        if (fields.Any(x => x.Count != 1)) throw new OAuthException("invalid_request");
    }
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (OAuthException e)
        {
            if (e.Error == "invalid_client" && Request.Headers.Authorization.ToString().StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            { Response.Headers.WWWAuthenticate = "Basic realm=\"MCP OAuth\""; return Unauthorized(new { error = e.Error }); }
            return BadRequest(new { error = e.Error });
        }
    }
}
public sealed record ConsentDecision(bool Approve, string[]? Scopes);
