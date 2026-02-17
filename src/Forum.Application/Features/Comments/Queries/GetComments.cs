using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves a paginated, filterable list of comments across all threads.
/// Uses CommentFilterParams to support pagination, sorting, and filtering.
/// </summary>
public record GetCommentsQuery(CommentFilterParams FilterParams) : IRequest<Result<PagedResult<CommentDto>>>;

/// <summary>
/// Handler for GetCommentsQuery.
/// Fetches a paged set of comments from the repository based on the provided filter parameters and maps them to DTOs.
/// </summary>
public class GetCommentsHandler : IRequestHandler<GetCommentsQuery, Result<PagedResult<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;

    public GetCommentsHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query by fetching a paged subset of comments from the database
    /// using the provided filter/pagination parameters, mapping each entity to a
    /// CommentDto, and returning the result wrapped in a PagedResult{T}.
    /// </summary>
    public async Task<Result<PagedResult<CommentDto>>> Handle(GetCommentsQuery request, CancellationToken cancellationToken)
    {
        // Fetch the paged comments and total count from the repository using filter params
        var (comments, totalCount) = await _commentRepository.GetPagedAsync(request.FilterParams, cancellationToken);

        // Map each Comment entity to a CommentDto (includes soft-delete display logic)
        var dtos = comments.Select(c => c.ToCommentDto()).ToList();

        // Wrap the results in a PagedResult with pagination metadata
        return Result.Success(PagedResult<CommentDto>.Create(
            dtos,
            totalCount,
            request.FilterParams.PageNumber,
            request.FilterParams.PageSize
        ));
    }
}

