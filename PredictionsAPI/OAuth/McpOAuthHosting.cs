using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using PredictionsAPI.Security;

namespace PredictionsAPI.OAuth;

public static class McpOAuthHosting
{
    public const string ChallengeScheme = "McpOAuthChallenge";
    public static IServiceCollection AddMcpOAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<McpOAuthOptions>().Bind(config.GetSection("Mcp:OAuth"))
            .Validate(x => x.IsValid(), "MCP OAuth requires canonical public origins and exact HTTPS callback URIs.").ValidateOnStart();
        services.AddScoped<McpOAuthService>();
        services.AddSingleton<IClientMetadataResolver, ClientMetadataResolver>();
        // Separate SDK metadata serving from the absolute challenge URL. TLS terminates at the proxy;
        // neither discovery nor issuer/audience construction may trust incoming Host/forwarded headers.
        services.AddAuthentication().AddMcp("McpOAuthMetadata", "MCP metadata", _ => { }).AddMcp(ChallengeScheme, "MCP OAuth", _ => { });
        services.AddOptions<McpAuthenticationOptions>("McpOAuthMetadata").Configure<IOptions<McpOAuthOptions>>((o, settings) =>
        {
            o.ForwardAuthenticate = McpAuthentication.Scheme;
            o.ResourceMetadataUri = new Uri("/.well-known/oauth-protected-resource", UriKind.Relative);
            o.ResourceMetadata = ResourceMetadata(settings.Value);
        });
        services.AddOptions<McpAuthenticationOptions>(ChallengeScheme).Configure<IOptions<McpOAuthOptions>>((o, settings) =>
        {
            o.ForwardAuthenticate = McpAuthentication.Scheme;
            o.ResourceMetadataUri = new Uri(settings.Value.Issuer + "/.well-known/oauth-protected-resource");
            o.ResourceMetadata = ResourceMetadata(settings.Value);
        });
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            o.AddPolicy("mcp-oauth", _ => RateLimitPartition.GetFixedWindowLimiter("oauth", _ => new()
            { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }
    private static ProtectedResourceMetadata ResourceMetadata(McpOAuthOptions options) => new()
    {
        Resource = options.Resource, AuthorizationServers = [options.Issuer],
        ScopesSupported = McpScopes.All.ToList(), ResourceName = "Predictions MCP"
    };

    public static IApplicationBuilder UseMcpOAuth(this IApplicationBuilder app)
    {
        // RFC 9728 permits both paths. The SDK handles the common canonical endpoint.
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/oauth") || path.StartsWithSegments("/api/auth/mcp-oauth") ||
                path.StartsWithSegments("/.well-known"))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                if (!context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Mcp:Enabled", true))
                { context.Response.StatusCode = 404; return; }
            }
            if (path == "/.well-known/oauth-protected-resource/mcp")
                context.Request.Path = "/.well-known/oauth-protected-resource";
            try { await next(context); }
            finally { context.Request.Path = path; }
        });
        return app;
    }
}
