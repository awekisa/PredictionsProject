namespace PredictionsAPI.DTOs.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
