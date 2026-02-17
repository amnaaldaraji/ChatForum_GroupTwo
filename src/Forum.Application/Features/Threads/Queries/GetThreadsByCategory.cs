using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves all threads in a specific category.
/// First validates that the category exists, then fetches its threads.
/// Used for the category detail page.
/// </summary>
public record GetThreadsByCategoryQuery(int CategoryId) : IRequest<Result<IReadOnlyList<ThreadSummaryDto>>>;

/// <summary>
/// Handler for GetThreadsByCategoryQuery.
/// Validates the category exists and fetches all its threads as summary DTOs.
/// </summary>
public class GetThreadsByCategoryHandler : IRequestHandler<GetThreadsByCategoryQuery, Result<IReadOnlyList<ThreadSummaryDto>>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICategoryRepository _categoryRepository;

    public GetThreadsByCategoryHandler(IThreadRepository threadRepository, ICategoryRepository categoryRepository)
    {
        _threadRepository = threadRepository;
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<IReadOnlyList<ThreadSummaryDto>>> Handle(GetThreadsByCategoryQuery request, CancellationToken cancellationToken)
    {
        if (!await _categoryRepository.ExistsAsync(c => c.CategoryId == request.CategoryId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ThreadSummaryDto>>("Category not found.");
        }

        // Fetch all threads in the category and map to summary DTOs
        var threads = await _threadRepository.GetByCategoryIdAsync(request.CategoryId, cancellationToken);
        var dtos = threads.Select(t => t.ToThreadSummaryDto()).ToList();
        return Result.Success<IReadOnlyList<ThreadSummaryDto>>(dtos);
    }
}

