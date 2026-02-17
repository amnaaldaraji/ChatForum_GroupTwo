namespace Forum.Application.DTOs.Category;

/// <summary>
/// Read-only DTO for returning category information to the client.
/// Includes the thread count for display in category listings.
/// </summary>
public record CategoryDto(
    int CategoryId,
    string Name,
    int ThreadCount
);