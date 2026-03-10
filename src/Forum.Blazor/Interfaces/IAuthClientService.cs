using Forum.Blazor.Models;

namespace Forum.Blazor.Interfaces;

/// <summary>
/// Client-side authentication service for login, registration, and token management.
/// </summary>
public interface IAuthClientService
{
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
}