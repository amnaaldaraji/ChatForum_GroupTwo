using System.Net.Http.Json;
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
    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            var request = new { Username = username, Password = password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(TryExtractError(errorBody) ?? "Invalid username or password.");
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
    public async Task<AuthResult> RegisterAsync(string username, string email, string password)
    {
        try
        {
            var request = new { Username = username, Email = email, Password = password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(TryExtractError(errorBody) ?? "Registration failed.");
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

    private static string? TryExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
            if (doc.RootElement.TryGetProperty("Message", out var msg2))
                return msg2.GetString();
        }
        catch { }
        return null;
    }
}
