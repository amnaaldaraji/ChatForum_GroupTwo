namespace Forum.Application.DTOs.Comment;

/// <summary>
/// DTO for updating an existing comment's content. Only the Content field
/// can be modified. 
/// Authorization checks (owner or admin) are performed in the command handler.
/// </summary>
/// <param name="Content">The new text content for the comment.</param>
public record UpdateCommentDto(string Content);