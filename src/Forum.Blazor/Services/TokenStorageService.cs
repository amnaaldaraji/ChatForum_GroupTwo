using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// Stores the JWT token in memory for the current circuit/session.
/// </summary>
public class TokenStorageService : ITokenStorageService
{
    private string? _token;

    /// <summary>
    /// Retrieves the stored JWT token, or null if none exists.
    /// </summary>
    public Task<string?> GetTokenAsync() => Task.FromResult(_token);

    /// <summary>
    /// Stores the JWT token. Called after successful login/register.
    /// </summary>
    public Task SetTokenAsync(string token)
    {
        _token = token;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the JWT token. Called on logout.
    /// </summary>
    public Task RemoveTokenAsync()
    {
        _token = null;
        return Task.CompletedTask;
    }
}
