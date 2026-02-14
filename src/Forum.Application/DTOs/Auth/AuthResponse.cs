namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO returned after successful login.
/// Includes JWT token to authenticate API calls.
/// </summary>
/// <param name="Token"> JWT Bearer token </param>
/// <param name="UserId"> ID of the authenticated user </param>
/// <param name="Username"> Authenticated user's display name </param>
/// <param name="Email">Authenticated user's email</param>
public record AuthResponse(string Token, string UserId, string Username, string? Email);