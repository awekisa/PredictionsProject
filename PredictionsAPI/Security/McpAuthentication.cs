using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PredictionsAPI.Services.Implementations;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Security;

public static class McpAuthentication
{
    public const string Scheme = "McpAccessToken";
    public const string TokenIdClaim = "mcp_token_id";
    public const string ScopeClaim = "mcp_scope";

    public static IServiceCollection AddMcpAccessTokens(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IMcpAccessTokenService, McpAccessTokenService>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, McpAccessTokenHandler>(Scheme, _ => { });
        services.AddScoped<IAuthorizationHandler, McpScopeHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var scope in McpScopes.All)
                options.AddPolicy(McpScopes.Policy(scope), policy => policy.AddAuthenticationSchemes(Scheme)
                    .RequireAuthenticatedUser().AddRequirements(new McpScopeRequirement(scope)));
        });
        return services;
    }
}

public class McpAccessTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IMcpAccessTokenService tokens) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Opt-in scheme for the MCP endpoint only; never a fallback for REST JWTs.
        if (!Request.Path.StartsWithSegments("/mcp")) return AuthenticateResult.NoResult();
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(header.Parameter))
            return AuthenticateResult.NoResult();
        var principal = await tokens.AuthenticateAsync(header.Parameter, Context.RequestAborted);
        return principal is null ? AuthenticateResult.Fail("Invalid or expired agent access token.") :
            AuthenticateResult.Success(new AuthenticationTicket(principal, McpAuthentication.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}

public record McpScopeRequirement(string Scope) : IAuthorizationRequirement;

public class McpScopeHandler(IMcpAccessTokenService tokens) : AuthorizationHandler<McpScopeRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, McpScopeRequirement requirement)
    {
        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? default;
        if (await tokens.HasPermissionAsync(context.User, requirement.Scope, cancellationToken)) context.Succeed(requirement);
    }
}
