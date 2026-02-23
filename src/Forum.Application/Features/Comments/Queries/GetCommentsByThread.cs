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
public record GetCommentsByThreadQuery(int ThreadId, int PageNumber = 1, int PageSize = 50) : IRequest<Result<PagedResult<CommentDto>>>;

/// <summary>
/// Handler for GetCommentsByThreadQuery.
/// Verifies the thread exists, then fetches and paginates the comments for that thread.
/// </summary>
public class GetCommentsByThreadHandler : IRequestHandler<GetCommentsByThreadQuery, Result<PagedResult<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IThreadRepository _threadRepository;

    public GetCommentsByThreadHandler(ICommentRepository commentRepository, IThreadRepository threadRepository)
    {
        _commentRepository = commentRepository;
        _threadRepository = threadRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<PagedResult<CommentDto>>> Handle(GetCommentsByThreadQuery request, CancellationToken cancellationToken)
    {
        if (!await _threadRepository.ExistsAsync(t => t.ThreadId == request.ThreadId, cancellationToken))
        {
            return Result.Failure<PagedResult<CommentDto>>("Thread not found.", ErrorType.NotFound);
        }

        // Build pagination params from the query parameters
        var paginationParams = new PaginationParams
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        // Fetch paged comments for this thread with their related data (User, ParentComment)
        var (comments, totalCount) = await _commentRepository.GetPagedByThreadIdAsync(
            request.ThreadId,
            paginationParams,
            cancellationToken);

        // Map each Comment entity to a CommentDto (includes soft-delete display logic)
        var dtos = comments.Select(c => c.ToCommentDto()).ToList();

        // Wrap the results in a PagedResult with pagination metadata
        return Result.Success(PagedResult<CommentDto>.Create(
            dtos,
            totalCount,
            paginationParams.PageNumber,
            paginationParams.PageSize
        ));
    }
}

