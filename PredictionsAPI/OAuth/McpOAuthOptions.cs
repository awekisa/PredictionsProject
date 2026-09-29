namespace PredictionsAPI.OAuth;

public sealed class McpOAuthOptions
{
    public string Issuer { get; set; } = "https://predictionsproject.onrender.com";
    public string WebsiteOrigin { get; set; } = "https://predictions-project.vercel.app";
    public string[] AllowedRedirectUris { get; set; } = ["https://claude.ai/api/mcp/auth_callback"];
    public string Resource => Issuer + "/mcp";

    public bool IsValid() => IsOrigin(Issuer) && IsOrigin(WebsiteOrigin) && AllowedRedirectUris.Length > 0 &&
        AllowedRedirectUris.All(x => Uri.TryCreate(x, UriKind.Absolute, out var u) && u.Scheme == "https" &&
            string.IsNullOrEmpty(u.Fragment) && string.IsNullOrEmpty(u.UserInfo) && !x.Contains('*'));
    private static bool IsOrigin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var u) &&
        (u.Scheme == "https" || (u.Scheme == "http" && u.IsLoopback)) && u.AbsolutePath == "/" &&
        string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) && string.IsNullOrEmpty(u.UserInfo) && !value.EndsWith('/');
}
