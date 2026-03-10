namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO representing a login request.
/// Sent by the client to authenticate and receive a JWT token.
/// </summary>
public record LoginRequest(string Username, string Password);