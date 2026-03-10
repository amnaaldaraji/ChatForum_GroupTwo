namespace Forum.Application.DTOs.Thread;

/// <summary>
/// DTO for updating an existing thread's metadata.
/// Both fields are nullable, to support partial updates — only provided fields are applied.
/// Updating the thread body is done by editing the first comment, not through this DTO.
/// </summary>
public record UpdateThreadDto(
    string? Title = null,
    int? CategoryId = null
);