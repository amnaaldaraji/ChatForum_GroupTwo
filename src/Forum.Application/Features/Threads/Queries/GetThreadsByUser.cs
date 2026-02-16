using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves a summary of a user's most recent threads.
/// Used for user profile pages to show recent thread activity.
/// </summary>
public record GetThreadsByUserQuery(string UserId, int Count = 10) : IRequest<Result<IReadOnlyList<ThreadSummaryDto>>>;

/// <summary>
/// Handler for GetThreadsByUserQuery.
/// Fetches the user's recent threads from the repository and maps them to summary DTOs.
/// </summary>
public class GetThreadsByUserHandler : IRequestHandler<GetThreadsByUserQuery, Result<IReadOnlyList<ThreadSummaryDto>>>
{
    private readonly IThreadRepository _threadRepository;

    public GetThreadsByUserHandler(IThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }

    /// <summary>
    /// Handles the query by fetching the specified number of recent threads
    /// for the given user and mapping them to ThreadSummaryDto objects.
    /// </summary>
    public async Task<Result<IReadOnlyList<ThreadSummaryDto>>> Handle(GetThreadsByUserQuery request, CancellationToken cancellationToken)
    {
        var threads = await _threadRepository.GetByUserIdAsync(request.UserId, request.Count, cancellationToken);
        var dtos = threads.Select(t => t.ToThreadSummaryDto()).ToList();
        return Result.Success<IReadOnlyList<ThreadSummaryDto>>(dtos);
    }
}