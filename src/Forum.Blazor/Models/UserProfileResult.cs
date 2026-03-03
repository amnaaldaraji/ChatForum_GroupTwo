namespace Forum.Blazor.Models;

public class UserProfileResult
{
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
