using Forum.Application.DTOs.Category;
using Forum.Application.DTOs.Comment;
using Forum.Application.DTOs.Thread;
using Forum.Application.DTOs.User;
using Forum.Domain.Entities;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Application.Mappings;

/// <summary>
/// Centralized extension methods for mapping domain entities to DTOs.
/// Entity-to-DTO conversion logic lives here, including soft-delete display rules.
/// </summary>
public static class MappingExtensions
{
    // Display name used when a user has been soft-deleted.
    private const string DeletedUserName = "Deleted User";

    // Content placeholder displayed in place of a soft-deleted comment's text.
    private const string DeletedCommentContent = "[deleted]";

    // Author name shown for soft-deleted comments (shorter than DeletedUserName).
    private const string DeletedAuthorName = "Deleted";

    /// <summary>
    /// Maps a Category entity to a CategoryDto, including the count of threads.
    /// </summary>
    public static CategoryDto ToCategoryDto(this Category category)
    {
        return new CategoryDto(
            category.CategoryId,
            category.Name,
            category.Threads?.Count ?? 0 
        );
    }

    /// <summary>
    /// Maps a Thread entity to a ThreadSummaryDto for use in thread listings.
    /// Checks if the author has been soft-deleted and replaces their name accordingly.
    /// </summary>
    public static ThreadSummaryDto ToThreadSummaryDto(this ThreadEntity thread)
    {
        var authorUserName = thread.User?.IsDeleted == true
            ? DeletedUserName
            : thread.User?.UserName ?? string.Empty;

        return new ThreadSummaryDto(
            thread.ThreadId,
            thread.Title,
            thread.UserId,
            authorUserName,
            thread.CategoryId,
            thread.Category?.Name ?? string.Empty, 
            thread.TimeCreated,
            thread.TimeUpdated,
            thread.Comments?.Count ?? 0 
        );
    }

    /// <summary>
    /// Maps a Thread entity to a ThreadDetailDto for the thread detail view.
    /// Separates the body comment (first comment) from the rest of the comments.
    /// </summary>
    public static ThreadDetailDto ToThreadDetailDto(this ThreadEntity thread, Comment? bodyComment, IReadOnlyList<CommentDto> comments)
    {
        var authorUserName = thread.User?.IsDeleted == true
            ? DeletedUserName
            : thread.User?.UserName ?? string.Empty;

        return new ThreadDetailDto(
            thread.ThreadId,
            thread.Title,
            thread.UserId,
            authorUserName,
            thread.CategoryId,
            thread.Category?.Name ?? string.Empty,
            thread.TimeCreated,
            thread.TimeUpdated,
            bodyComment?.ToCommentDto(),
            comments
        );
    }

    /// <summary>
    /// Maps a Comment entity to a CommentDto. Implements the soft-delete display logic.
    /// </summary>
    public static CommentDto ToCommentDto(this Comment comment)
    {
        var isDeleted = comment.IsDeleted;
        var authorIsDeleted = comment.User?.IsDeleted == true;
        
        var content = isDeleted ? DeletedCommentContent : comment.Content;

        // Determine the author display name based on deletion states:
        var authorUserName = isDeleted
            ? DeletedAuthorName
            : authorIsDeleted
                ? DeletedUserName
                : comment.User?.UserName ?? string.Empty;

        // Resolve parent comment author name for "replying to @username" display
        // Also handles the case where the parent comment's author has been soft-deleted
        var parentAuthorUserName = comment.ParentComment?.User?.IsDeleted == true
            ? DeletedUserName
            : comment.ParentComment?.User?.UserName;

        return new CommentDto(
            comment.CommentId,
            content,
            comment.UserId,
            authorUserName,
            comment.ThreadId,
            comment.ParentCommentId,
            parentAuthorUserName,
            comment.TimeCreated,
            comment.Replies?.Count ?? 0, // Count of direct replies
            isDeleted
        );
    }

    /// <summary>
    /// Maps a User entity to a UserDto. Applies soft-delete display rules.
    /// </summary>
    public static UserDto ToUserDto(this User user)
    {
        var userName = user.IsDeleted ? DeletedUserName : user.UserName ?? string.Empty;
        return new UserDto(
            user.Id,
            userName,
            user.IsDeleted ? null : user.Email, // Hide email for deleted users (privacy)
            user.Threads?.Count ?? 0,
            user.Comments?.Count ?? 0,
            user.IsDeleted
        );
    }

    /// <summary>
    /// Maps a User entity to a UserProfileDto, which includes recent activity.
    /// </summary>
    public static UserProfileDto ToUserProfileDto(
        this User user,
        IReadOnlyList<ThreadSummaryDto> recentThreads,
        IReadOnlyList<CommentDto> recentComments)
    {
        var userName = user.IsDeleted ? DeletedUserName : user.UserName ?? string.Empty;
        return new UserProfileDto(
            user.Id,
            userName,
            user.IsDeleted ? null : user.Email, // Hide email for deleted users
            user.Threads?.Count ?? 0,
            user.Comments?.Count ?? 0,
            user.IsDeleted,
            recentThreads,
            recentComments
        );
    }
}

