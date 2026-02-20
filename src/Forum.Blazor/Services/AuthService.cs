using System.Net.Http.Json;
using Forum.Application.DTOs.Auth;

namespace Forum.Blazor.Services;

/// <summary>
/// Handles authentication operations (login, register, logout) against the Forum.Api.
/// </summary>
public class AuthService : ApiClientBase
{
    private readonly TokenStorageService _tokenStorage;
    private readonly ApiAuthenticationStateProvider _authStateProvider;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        TokenStorageService tokenStorage,
        ApiAuthenticationStateProvider authStateProvider)
        : base(httpClientFactory, tokenStorage)
    {
        _tokenStorage = tokenStorage;
        _authStateProvider = authStateProvider;
    }

    /// <summary>
    /// Sends credentials to POST /api/auth/login.
    /// </summary>
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var response = await PostAsync("api/auth/login", request);
        if (!response.IsSuccessStatusCode)
            return null;

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        if (authResponse != null)
        {
            await _tokenStorage.SetTokenAsync(authResponse.Token);
            _authStateProvider.NotifyUserAuthentication(authResponse.Token);
        }

        return authResponse;
    }

    /// <summary>
    /// Sends registration data to POST /api/auth/register.
    /// </summary>
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        var response = await PostAsync("api/auth/register", request);
        if (!response.IsSuccessStatusCode)
            return null;

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        if (authResponse != null)
        {
            await _tokenStorage.SetTokenAsync(authResponse.Token);
            _authStateProvider.NotifyUserAuthentication(authResponse.Token);
        }

        return authResponse;
    }

    /// <summary>
    /// Clears the stored JWT and resets the authentication state to anonymous.
    /// </summary>
    public async Task LogoutAsync()
    {
        await _tokenStorage.ClearTokenAsync();
        _authStateProvider.NotifyUserLogout();
    }
}
