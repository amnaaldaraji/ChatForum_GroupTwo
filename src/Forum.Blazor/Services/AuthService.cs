using System.Net.Http.Json;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

    /// <summary>
    /// GET current user's profile by extracting the user id from the stored JWT
    /// and calling GET /api/users/{id}.
    /// </summary>
    public async Task<UserProfileResult> GetProfileAsync()
    {
        try
        {
            var userId = await GetUserIdFromTokenAsync();
            if (string.IsNullOrEmpty(userId))
                return new UserProfileResult { Succeeded = false, ErrorMessage = "Not authenticated." };

            var dto = await _httpClient.GetFromJsonAsync<UserProfileDto>($"api/users/{userId}", JsonOptions);
            if (dto is null)
                return new UserProfileResult { Succeeded = false, ErrorMessage = "Failed to load profile." };

            return new UserProfileResult
            {
                Succeeded = true,
                Username = dto.Username ?? string.Empty,
                Email = dto.Email ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            return new UserProfileResult { Succeeded = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Updates the authenticated user's email via PUT /api/users/{id}.
    /// </summary>
    public async Task<AuthResult> UpdateEmailAsync(string newEmail)
    {
        try
        {
            var userId = await GetUserIdFromTokenAsync();
            if (string.IsNullOrEmpty(userId))
                return AuthResult.Failure("Not authenticated.");

            var dto = new { Email = newEmail };
            var response = await _httpClient.PutAsJsonAsync($"api/users/{userId}", dto, JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(TryExtractError(body) ?? "Failed to update email.");
            }

            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Update email failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Changes the authenticated user's password.
    /// Posts to POST /api/auth/change-password (adjust route if your API differs).
    /// </summary>
    public async Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            var request = new { CurrentPassword = currentPassword, NewPassword = newPassword };
            var response = await _httpClient.PostAsJsonAsync("api/auth/change-password", request, JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(TryExtractError(body) ?? "Failed to change password.");
            }

            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Change password failed: {ex.Message}");
        }
    }

    private static string? TryExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString();
            if (doc.RootElement.TryGetProperty("title", out var title))
                return title.GetString();
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Extracts the user id (sub claim) from the stored JWT token.
    /// </summary>
    private async Task<string?> GetUserIdFromTokenAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            // Prefer "sub", fallback to common claim types
            var idClaim = jwt.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid");
            return idClaim?.Value;
        }
        catch
        {
            return null;
        }
    }

    // Minimal DTO used to read /api/users/{id} response
    private class UserProfileDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
    }
}
