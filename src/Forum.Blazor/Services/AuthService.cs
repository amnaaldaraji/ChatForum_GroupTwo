using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
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
    /// Returns tuple (AuthResponse? response, string? errorMessage).
    /// On non-success tries to extract error details returned by the API.
    /// Posts as unauthenticated client because register is public.
    /// Adds diagnostic logging to help find why server reports password errors.
    /// </summary>
    public async Task<(AuthResponse? Response, string? ErrorMessage)> RegisterAsync(RegisterRequest request)
    {
        // Diagnostic: log the outgoing payload so you can verify what is sent to the API.
        try
        {
            Console.WriteLine("Register payload: " + JsonSerializer.Serialize(request, JsonOptions));
        }
        catch { /* ignore logging errors */ }

        // Use unauthenticated client for register endpoint
        var client = await CreateClientAsync(authenticate: false);
        var response = await client.PostAsJsonAsync("api/auth/register", request, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            // Try to read structured errors { Errors = [...] }
            try
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement?>(JsonOptions);
                if (json.HasValue && json.Value.TryGetProperty("Errors", out var errorsEl) && errorsEl.ValueKind == JsonValueKind.Array)
                {
                    var errors = errorsEl.EnumerateArray()
                                         .Select(e => e.GetString())
                                         .Where(s => !string.IsNullOrEmpty(s));
                    var joined = string.Join("; ", errors);
                    Console.WriteLine("Register error (structured): " + joined);
                    return (null, joined);
                }
            }
            catch
            {
                // fall back to raw text
            }

            var text = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Register error (raw): " + text);
            return (null, string.IsNullOrWhiteSpace(text) ? response.ReasonPhrase : text);
        }

        // success path: try deserialize token response
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        if (authResponse != null)
        {
            await _tokenStorage.SetTokenAsync(authResponse.Token);
            _authStateProvider.NotifyUserAuthentication(authResponse.Token);
            return (authResponse, null);
        }

        // success but no token returned — return placeholder
        return (new AuthResponse(string.Empty, string.Empty, request.Username, request.Email), null);
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