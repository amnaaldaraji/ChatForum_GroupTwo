namespace Forum.Application.DTOs.Thread;

/// <summary>
/// DTO for creating a new thread. The Body field will be stored as the first
/// comment in the thread, not as a field on Thread itself.
/// The UserId of the creator is extracted from the JWT token at the API layer.
/// </summary>
public record CreateThreadDto(
    string Title,
    int CategoryId,
    string Body
);