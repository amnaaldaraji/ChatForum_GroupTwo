namespace Forum.Application.DTOs.Comment;

/// <summary>
/// DTO for updating an existing comment's content.
/// Only the Content field can be modified. 
/// Authorization checks (owner or admin) are performed in the command handler.
/// </summary>
public record UpdateCommentDto(string Content);