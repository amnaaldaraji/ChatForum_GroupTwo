namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO representing a registration request.
/// Sent by the client when creating a new account.
/// </summary>
/// <param name="Username"> Desired name </param>
/// <param name="Email"> User's email </param>
/// <param name="Password"> Desired password (has to meet requirements from Identity </param>
public record RegisterRequest(string Username, string Email, string Password);