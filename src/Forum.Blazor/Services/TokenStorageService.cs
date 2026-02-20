using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Forum.Blazor.Services;

/// <summary>
/// Wraps ProtectedSessionStorage to persist the JWT token across page navigations within the same browser session.
/// </summary>
public class TokenStorageService
{
    private const string TokenKey = "auth_token";
    private readonly ProtectedSessionStorage _sessionStorage;

    public TokenStorageService(ProtectedSessionStorage sessionStorage)
    {
        _sessionStorage = sessionStorage;
    }

    /// <summary>
    /// Retrieves the stored JWT token, or null if none exists or during prerender.
    /// </summary>
    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var result = await _sessionStorage.GetAsync<string>(TokenKey);
            return result.Success ? result.Value : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stores the JWT token in encrypted session storage. Called after successful login/register.
    /// </summary>
    public async Task SetTokenAsync(string token)
    {
        try
        {
            await _sessionStorage.SetAsync(TokenKey, token);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// Removes the JWT token from session storage. Called on logout.
    /// </summary>
    public async Task ClearTokenAsync()
    {
        try
        {
            await _sessionStorage.DeleteAsync(TokenKey);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
