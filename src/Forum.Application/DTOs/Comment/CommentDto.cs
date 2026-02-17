namespace Forum.Application.DTOs.Comment;

/// <summary>
/// Read-only DTO for returning comment data to the client.
/// Handles soft-delete display logic: if IsDeleted is true, Content shows "[deleted]"
/// and AuthorUserName shows "Deleted" (applied in MappingExtensions).
///
/// Includes parent comment info to support the flat reply display model
/// ("replying to @username").
/// </summary>
public record CommentDto(
    int CommentId,
    string Content,
    string AuthorId,
    string AuthorUserName,
    int ThreadId,
    int? ParentCommentId,
    string? ParentCommentAuthorUserName,
    DateTime TimeCreated,
    int ReplyCount,
    bool IsDeleted
);