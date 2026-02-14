namespace Forum.Application.DTOs.User;

/// <summary>
/// DTO for updating a user's profile.
/// Both fields are nullable, to support partial updates — only provided fields are applied.
/// The user ID is extracted from the JWT token at the API layer.
/// </summary>
public record UpdateUserProfileDto(
    string? UserName = null,
    string? Email = null
);