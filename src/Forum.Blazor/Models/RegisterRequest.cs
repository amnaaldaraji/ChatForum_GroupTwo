namespace Forum.Blazor.Models;

/// <summary>
/// Model for user registration form data. ConfirmPassword is used for client-side
/// validation only and is excluded when sending to POST /api/auth/register.
/// </summary>
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
