namespace Forum.Blazor.Interfaces;

/// <summary>
/// Persists and retrieves the JWT authentication token for the current session.
/// </summary>
public interface ITokenStorageService
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
}
