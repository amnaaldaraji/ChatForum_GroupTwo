using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves a paginated list of comments belonging to a specific thread.
/// Used by the thread detail page to display the discussion.
/// </summary>
public record GetCommentsByThreadQuery(
    int ThreadId,
    int PageNumber = 1,
    int PageSize = 50,
    string? CurrentUserId = null) : IRequest<Result<PagedResult<CommentDto>>>;

/// <summary>
/// Handler for GetCommentsByThreadQuery.
/// Verifies the thread exists, then fetches and paginates the comments for that thread.
/// </summary>
public class GetCommentsByThreadHandler : IRequestHandler<GetCommentsByThreadQuery, Result<PagedResult<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly IVoteRepository _voteRepository;

    public GetCommentsByThreadHandler(
        ICommentRepository commentRepository,
        IThreadRepository threadRepository,
        IVoteRepository voteRepository)
    {
        _commentRepository = commentRepository;
        _threadRepository = threadRepository;
        _voteRepository = voteRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<PagedResult<CommentDto>>> Handle(GetCommentsByThreadQuery request, CancellationToken cancellationToken)
    {
        if (!await _threadRepository.ExistsAsync(t => t.ThreadId == request.ThreadId, cancellationToken))
            return Result.Failure<PagedResult<CommentDto>>("Thread not found.", ErrorType.NotFound);

        var paginationParams = new PaginationParams
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        var (comments, totalCount) = await _commentRepository.GetPagedByThreadIdAsync(
            request.ThreadId, paginationParams, cancellationToken);

        // Batch-load vote data (2 queries total, not per-comment)
        var commentIds = comments.Select(c => c.CommentId).ToList();
        var scores = await _voteRepository.GetScoresForCommentsAsync(commentIds, cancellationToken);

        Dictionary<int, int> userVotes = request.CurrentUserId != null
            ? await _voteRepository.GetUserVotesForCommentsAsync(request.CurrentUserId, commentIds, cancellationToken)
            : new();

        var dtos = comments.Select(c => c.ToCommentDto(
            scores.GetValueOrDefault(c.CommentId, 0),
            request.CurrentUserId != null ? userVotes.GetValueOrDefault(c.CommentId, 0) : null
        )).ToList();

        return Result.Success(PagedResult<CommentDto>.Create(
            dtos, totalCount, paginationParams.PageNumber, paginationParams.PageSize));
    }
}