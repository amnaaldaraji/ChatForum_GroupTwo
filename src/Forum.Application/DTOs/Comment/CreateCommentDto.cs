namespace Forum.Application.DTOs.Comment;

/// <summary>
/// DTO for creating a new comment in a thread.
/// The ThreadId is provided as a route parameter, and the UserId is extracted from the JWT token.
/// </summary>
/// <param name="Content">The text content of the comment.</param>
/// <param name="ParentCommentId">Optional: the ID of the comment being replied to. Null for top-level comments.</param>
public record CreateCommentDto(
    string Content,
    int? ParentCommentId = null
);