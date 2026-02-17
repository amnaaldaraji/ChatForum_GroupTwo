namespace Forum.Application.DTOs.User;

/// <summary>
/// Read-only DTO for returning basic user information.
/// Used in user listings and references. Handles soft-delete display: if IsDeleted, UserName shows
/// "Deleted User" and Email is null (applied in MappingExtensions).
/// </summary>
public record UserDto(
    string Id,
    string UserName,
    string? Email,
    int ThreadCount,
    int CommentCount,
    bool IsDeleted
);