using Forum.Application.DTOs.Comment;
using Forum.Application.DTOs.Thread;

namespace Forum.Application.DTOs.User;

/// <summary>
/// Extended user DTO for profile pages. Includes all basic user info plus
/// the user's recent threads and comments for displaying activity history.
/// </summary>
public record UserProfileDto(
    string Id,
    string UserName,
    string? Email,
    int ThreadCount,
    int CommentCount,
    bool IsDeleted,
    IReadOnlyList<ThreadSummaryDto> RecentThreads,
    IReadOnlyList<CommentDto> RecentComments
);