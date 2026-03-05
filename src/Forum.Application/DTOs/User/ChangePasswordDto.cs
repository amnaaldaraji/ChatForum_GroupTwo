namespace Forum.Application.DTOs.User;

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);
