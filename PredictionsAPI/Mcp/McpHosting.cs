using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using PredictionsAPI.Security;

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
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, ct) =>
            {
                var services = context.Services!;
                var mutation = context.Params.Name == "save_my_prediction";
                var result = "failed";
                try
                {
                    var scope = mutation ? McpScopes.PredictionsWrite : McpScopes.AppRead;
                    var auth = await services.GetRequiredService<IAuthorizationService>()
                        .AuthorizeAsync(context.User!, null, McpScopes.Policy(scope));
                    if (!auth.Succeeded)
                    {
                        result = "denied";
                        return Error($"This connection requires the {scope} scope.");
                    }
                    if (context.MatchedPrimitive is not McpServerTool tool) return Error("Unknown tool.");
                    ValidateArguments(tool.ProtocolTool.InputSchema, context.Params.Arguments);
                    var response = await next(context, ct);
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
                        int? target = context.Params.Arguments?.TryGetValue("gameId", out var id) == true && id.ValueKind == JsonValueKind.Number && id.TryGetInt32(out var value) ? value : null;
                        var http = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
                        services.GetRequiredService<ILoggerFactory>().CreateLogger("Predictions.Mcp.Audit").LogInformation(
                            "MCP mutation ActorId={ActorId} TokenId={TokenId} Tool={Tool} TargetId={TargetId} Timestamp={Timestamp} Result={Result} CorrelationId={CorrelationId}",
                            context.User?.FindFirstValue(ClaimTypes.NameIdentifier), context.User?.FindFirstValue(McpAuthentication.TokenIdClaim),
                            "save_my_prediction", target, services.GetRequiredService<TimeProvider>().GetUtcNow(), result, http?.TraceIdentifier);
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

    private static void ValidateArguments(JsonElement schema, IDictionary<string, JsonElement>? arguments)
    {
        arguments ??= new Dictionary<string, JsonElement>();
        if (schema.TryGetProperty("required", out var required))
            foreach (var key in required.EnumerateArray())
                if (!arguments.ContainsKey(key.GetString()!)) throw new ToolInputException($"Missing required argument: {key.GetString()}.");
        var properties = schema.GetProperty("properties");
        foreach (var (name, value) in arguments)
        {
            if (!properties.TryGetProperty(name, out _)) throw new ToolInputException("Unknown argument. Use only the arguments in the tool schema.");
            if (name is "userDisplayName" or "type")
            {
                if (value.ValueKind != JsonValueKind.String) throw new ToolInputException($"{name} must be a string.");
                continue;
            }
            if (name == "tournamentId" && value.ValueKind == JsonValueKind.Null &&
                (required.ValueKind != JsonValueKind.Array || !required.EnumerateArray().Any(x => x.GetString() == name))) continue;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
                throw new ToolInputException($"{name} must be an integer.");
            if (name == "limit" ? number is < 1 or > 100 : name is "gameId" or "tournamentId" ? number <= 0 : number < 0)
                throw new ToolInputException(name == "limit" ? "limit must be between 1 and 100." : $"{name} is outside the allowed range.");
        }
    }
}
