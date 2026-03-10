namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO representing a registration request.
/// Sent by the client when creating a new account.
/// </summary>
public record RegisterRequest(string Username, string Email, string Password);