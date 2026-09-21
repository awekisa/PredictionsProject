using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using PredictionsAPI.Security;
using PredictionsAPI.Data;

namespace PredictionsAPI.Mcp;

public static class McpHosting
{
    public static IServiceCollection AddPredictionsMcp(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        // SDK diagnostics can include arguments/exception details at verbose levels. Audit metadata only.
        services.AddLogging(logging => logging.AddFilter("ModelContextProtocol", LogLevel.None));
        services.AddMcpServer(options => options.ServerInfo = new() { Name = "Predictions", Version = "1.0.0" })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<PredictionTools>()
            .WithTools<AdminTools>()
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, ct) =>
            {
                var services = context.Services!;
                var tool = context.MatchedPrimitive as McpServerTool;
                var operation = tool?.Metadata.OfType<McpOperationAttribute>().SingleOrDefault();
                var mutation = operation?.Mutates == true;
                object? target = null;
                var result = "failed";
                try
                {
                    if (tool is null || operation is null) return Error("Unknown tool.");
                    var scope = operation.Scope;
                    target = await AuditTargetAsync(operation.TargetArgument, context.Params.Arguments, services, lookupUser: false);
                    var auth = await services.GetRequiredService<IAuthorizationService>()
                        .AuthorizeAsync(context.User!, null, McpScopes.Policy(scope));
                    if (!auth.Succeeded)
                    {
                        result = "denied";
                        return Error($"This connection requires the {scope} scope" + (McpScopes.IsAdmin(scope) ? " and a current Admin role." : "."));
                    }
                    target ??= await AuditTargetAsync(operation.TargetArgument, context.Params.Arguments, services);
                    McpArgumentValidation.Validate(tool.ProtocolTool.InputSchema, context.Params.Arguments);

                    var response = await next(context, ct);
                    if (mutation && response.IsError != true && context.Params.Name is "admin_create_tournament" or "admin_create_game")
                        target = response.StructuredContent?.GetProperty("id").ToString();
                    result = response.IsError == true ? "failed" : "succeeded";
                    return response;
                }
                catch (ToolInputException ex)
                {
                    result = "rejected";
                    return Error(ex.Message);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    result = "cancelled";
                    throw;
                }
                catch (Exception)
                {
                    // Do not expose or log exception messages: upstream providers may include secrets.
                    return Error("The application could not complete this request. Check the current state before trying again.");
                }
                finally
                {
                    if (mutation)
                    {
                        var http = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
                        services.GetRequiredService<ILoggerFactory>().CreateLogger("Predictions.Mcp.Audit").LogInformation(
                            "MCP mutation ActorId={ActorId} TokenId={TokenId} Tool={Tool} TargetId={TargetId} Timestamp={Timestamp} Result={Result} CorrelationId={CorrelationId}",
                            context.User?.FindFirstValue(ClaimTypes.NameIdentifier), context.User?.FindFirstValue(McpAuthentication.TokenIdClaim),
                            tool!.ProtocolTool.Name, target, services.GetRequiredService<TimeProvider>().GetUtcNow(), result, http?.TraceIdentifier);
                    }
                }
            }));
        return services;
    }

    // Install before auth/CORS. Exact origins only; no-Origin native clients are allowed.
    public static IApplicationBuilder UsePredictionsMcpOriginValidation(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/mcp"))
        {
            context.Response.Headers.CacheControl = "no-store";
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            if (!config.GetValue("Mcp:Enabled", true))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            var allowed = config.GetSection("Mcp:AllowedOrigins").Get<string[]>()
                ?? config["CorsOrigins"]?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                ?? ["http://localhost:5173"];
            if (context.Request.Headers.TryGetValue("Origin", out var origin) &&
                (origin.Count != 1 || !allowed.Contains(origin[0], StringComparer.Ordinal)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        await next(context);
    });

    public static void MapPredictionsMcp(this IEndpointRouteBuilder endpoints) => endpoints.MapMcp("/mcp")
        .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = McpAuthentication.Scheme });

    private static CallToolResult Error(string message) => new()
    {
        IsError = true, Content = [new TextContentBlock { Text = message }]
    };

    private static async Task<object?> AuditTargetAsync(string? path, IDictionary<string, JsonElement>? arguments, IServiceProvider services, bool lookupUser = true)
    {
        if (path is null || arguments is null) return null;
        var parts = path.Split('.');
        if (!arguments.TryGetValue(parts[0], out var value)) return null;
        foreach (var part in parts.Skip(1))
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id)) return id;
        // Only log real account IDs, never an arbitrary supplied string on a missing target.
        if (lookupUser && path == "userId" && value.ValueKind == JsonValueKind.String)
            return (await services.GetRequiredService<AppDbContext>().Users.FindAsync(value.GetString()))?.Id;
        return null;
    }
}
