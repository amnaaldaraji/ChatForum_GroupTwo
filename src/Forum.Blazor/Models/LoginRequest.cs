namespace Forum.Blazor.Models;

/// <summary>
/// Model for user login form data, sent to POST /api/auth/login.
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
