namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO returned after successful login.
/// Includes JWT token to authenticate API calls.
/// </summary>
public record AuthResponse(string Token, string UserId, string Username, string? Email);