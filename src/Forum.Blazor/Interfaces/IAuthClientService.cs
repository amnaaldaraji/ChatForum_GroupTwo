using Forum.Blazor.Models;

namespace Forum.Blazor.Interfaces;

public interface IAuthClientService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<AuthResult> RegisterAsync(string username, string email, string password);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();

    // Added: used by Profile.razor
    Task<UserProfileResult> GetProfileAsync();
    Task<AuthResult> UpdateEmailAsync(string newEmail);
    Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword);
}
