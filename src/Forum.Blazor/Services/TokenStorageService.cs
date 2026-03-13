using Forum.Blazor.Interfaces;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Forum.Blazor.Services;

/// <summary>
/// Stores the JWT token in the browser's protected session storage,
/// with an in-memory cache to avoid JS interop on every read.
/// </summary>
public class TokenStorageService : ITokenStorageService
{
    private const string StorageKey = "auth_token";
    private readonly ProtectedSessionStorage _sessionStorage;
    private string? _cache;

    public TokenStorageService(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    /// <summary>
    /// Retrieves the stored JWT token, or null if none exists.
    /// Falls back during prerendering when JS interop is unavailable.
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        if (_cache is not null)
            return _cache;

        try
        {
            var result = await _sessionStorage.GetAsync<string>(StorageKey);
            _cache = result.Success ? result.Value : null;
            return _cache;
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender — treat as unauthenticated.
            return null;
        }
    }

    /// <summary>
    /// Stores the JWT token. Called after successful login/register.
    /// </summary>
    public async Task SetTokenAsync(string token)
    {
        _cache = token;
        await _sessionStorage.SetAsync(StorageKey, token);
    }

    /// <summary>
    /// Removes the JWT token. Called on logout.
    /// </summary>
    public async Task RemoveTokenAsync()
    {
        _cache = null;
        await _sessionStorage.DeleteAsync(StorageKey);
    }
}