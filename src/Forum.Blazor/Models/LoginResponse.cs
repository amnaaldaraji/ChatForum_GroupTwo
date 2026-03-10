namespace Forum.Blazor.Models;

/// <summary>
/// Response returned by the API on successful login, containing the JWT token and user details.
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
}
