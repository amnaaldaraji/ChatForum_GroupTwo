using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves a paginated, filtered, and sorted list of threads.
/// Supports filtering by category, author, search term, date range, and sort order.
/// Used by the main thread listing page and search functionality.
/// </summary>
public record GetThreadsQuery(ThreadFilterParams FilterParams) : IRequest<Result<PagedResult<ThreadSummaryDto>>>;

/// <summary>
/// Handler for GetThreadsQuery.
/// Delegates filtering/pagination to the repository and maps the results to summary DTOs wrapped in a PagedResult.
/// </summary>
public class GetThreadsHandler : IRequestHandler<GetThreadsQuery, Result<PagedResult<ThreadSummaryDto>>>
{
    private readonly IThreadRepository _threadRepository;

    public GetThreadsHandler(IThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<PagedResult<ThreadSummaryDto>>> Handle(GetThreadsQuery request, CancellationToken cancellationToken)
    {
        // Fetch paged threads; repository handles filtering, sorting, and pagination
        var (threads, totalCount) = await _threadRepository.GetPagedAsync(request.FilterParams, cancellationToken);

        // Map Thread entities to summary DTOs
        var dtos = threads.Select(t => t.ToThreadSummaryDto()).ToList();

        // Wrap in PagedResult with pagination metadata
        return Result.Success(PagedResult<ThreadSummaryDto>.Create(
            dtos,
            totalCount,
            request.FilterParams.PageNumber,
            request.FilterParams.PageSize
        ));
    }
}

