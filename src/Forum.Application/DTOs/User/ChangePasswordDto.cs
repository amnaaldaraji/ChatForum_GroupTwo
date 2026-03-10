namespace Forum.Application.DTOs.User;

/// <summary>
/// Payload for changing a user's password.
/// </summary>
public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);
