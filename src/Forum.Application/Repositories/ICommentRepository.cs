using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom comment-specific methods.
/// </summary>
public interface ICommentRepository : IRepository<Comment>
{
    Task<Comment?> GetByIdWithDetailsAsync(int commentId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedByThreadIdAsync(
        int threadId,
        PaginationParams paginationParams,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedAsync(
        CommentFIlterParams filterParams,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Comment>> GetUserByIdAsync(int userId,int count, CancellationToken cancellationToken = default);
    Task<Comment?> GetFirstCommentByThreadIdAsync(int threadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Comment>> GetRepliesAsync (int commentId, CancellationToken cancellationToken = default);
}