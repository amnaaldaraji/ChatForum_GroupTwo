using System.Text.Json;
using Forum.Blazor.Interfaces;
using Forum.Blazor.Models;

namespace Forum.Blazor.Services;

/// <summary>
/// Handles authentication operations (login, register, logout) against the Forum.Api.
/// </summary>
public class AuthService : IAuthClientService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;
    private readonly ApiAuthenticationStateProvider _authStateProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthService(
        HttpClient httpClient,
        ITokenStorageService tokenStorage,
        ApiAuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _authStateProvider = authStateProvider;
    }

    /// <summary>
    /// Sends credentials to POST /api/auth/login.
    /// </summary>
    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { request.Username, request.Password });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(ApiClientBase.TryExtractError(errorBody) ?? "Invalid username or password.");
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            if (loginResponse is null || string.IsNullOrEmpty(loginResponse.Token))
                return AuthResult.Failure("Login failed: no token received.");

            await _tokenStorage.SetTokenAsync(loginResponse.Token);
            _authStateProvider.MarkUserAsAuthenticated(loginResponse.Token);

            return AuthResult.Success(loginResponse);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Login failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends registration data to POST /api/auth/register.
    /// </summary>
    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", new { request.Username, request.Email, request.Password });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(ApiClientBase.TryExtractError(errorBody) ?? "Registration failed.");
            }

            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Registration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the stored JWT and resets the authentication state to anonymous.
    /// </summary>
    public async Task LogoutAsync()
    {
        await _tokenStorage.RemoveTokenAsync();
        _authStateProvider.MarkUserAsLoggedOut();
    }

    public Task<string?> GetTokenAsync() => _tokenStorage.GetTokenAsync();

}
