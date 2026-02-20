namespace Forum.Application.DTOs.Thread;

/// <summary>
/// Lightweight DTO used in thread listings (e.g., category page, search results).
/// Contains summary information without the full thread body or comments.
/// </summary>
public record ThreadSummaryDto(
    int ThreadId,
    string Title,
    string AuthorId,
    string AuthorUserName,
    int CategoryId,
    string CategoryName,
    DateTime TimeCreated,
    DateTime TimeUpdated,
    int CommentCount,
    string? LastPosterUserName,
    DateTime? LastCommentTime
);