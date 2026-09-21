namespace PredictionsAPI.Security;

public static class McpScopes
{
    public const string AppRead = "app:read";
    public const string PredictionsWrite = "predictions:write";
    public const string AdminRead = "admin:read";
    public const string AdminWrite = "admin:write";
    public static readonly IReadOnlyList<string> All =
        Array.AsReadOnly(new[] { AppRead, PredictionsWrite, AdminRead, AdminWrite });

    public static bool IsAdmin(string scope) => scope is AdminRead or AdminWrite;
    public static string Policy(string scope) => $"Mcp:{scope}";
}
