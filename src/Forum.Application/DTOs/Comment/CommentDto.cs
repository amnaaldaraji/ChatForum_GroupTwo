namespace Forum.Application.DTOs.Comment;

/// <summary>
/// Read-only DTO for returning comment data to the client.
/// Handles soft-delete display logic: if IsDeleted is true, Content shows "[deleted]"
/// and AuthorUserName shows "Deleted" (applied in MappingExtensions).
/// Includes parent comment info to support the flat reply display model
/// with an inline citation of the parent comment.
/// </summary>
public record CommentDto(
    int CommentId,
    string Content,
    string AuthorId,
    string AuthorUserName,
    int ThreadId,
    string? ThreadTitle,
    int? ParentCommentId,
    string? ParentCommentAuthorUserName,
    string? ParentCommentContent,
    DateTime TimeCreated,
    int ReplyCount,
    bool IsDeleted,
    int VoteScore,
    int? CurrentUserVote
);