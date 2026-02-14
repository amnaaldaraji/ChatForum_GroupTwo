using Forum.Application.DTOs.Comment;

namespace Forum.Application.DTOs.Thread;

/// <summary>
/// Full-detail DTO used when viewing a single thread. Includes the thread metadata,
/// the body comment, and all subsequent comments.
/// The BodyComment is separated from Comments to allow distinct rendering:
/// the body is displayed as the thread opener, while Comments are the replies below it.
/// </summary>
public record ThreadDetailDto(
    int ThreadId,
    string Title,
    string AuthorId,
    string AuthorUserName,
    int CategoryId,
    string CategoryName,
    DateTime TimeCreated,
    DateTime TimeUpdated,
    CommentDto? BodyComment,
    IReadOnlyList<CommentDto> Comments
);