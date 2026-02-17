using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves the most recent comments made by a specific user.
/// Used for user profile pages to display recent activity. Does not paginate.
/// </summary>
public record GetCommentsByUserQuery(string UserId, int Count = 10) : IRequest<Result<IReadOnlyList<CommentDto>>>;

/// <summary>
/// Handler for GetCommentsByUserQuery.
/// Fetches a limited number of recent comments for the specified user and maps them to DTOs.
/// </summary>
public class GetCommentsByUserHandler : IRequestHandler<GetCommentsByUserQuery, Result<IReadOnlyList<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;

    public GetCommentsByUserHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query by fetching up to Count recent comments for the user
    /// from the repository and mapping each to a CommentDto.
    /// </summary>
    public async Task<Result<IReadOnlyList<CommentDto>>> Handle(GetCommentsByUserQuery request, CancellationToken cancellationToken)
    {
        // Fetch the user's most recent comments (limited by Count), ordered by creation date
        var comments = await _commentRepository.GetByUserIdAsync(request.UserId, request.Count, cancellationToken);

        // Map each Comment entity to a CommentDto (includes soft-delete display logic)
        var dtos = comments.Select(c => c.ToCommentDto()).ToList();

        return Result.Success<IReadOnlyList<CommentDto>>(dtos);
    }
}

