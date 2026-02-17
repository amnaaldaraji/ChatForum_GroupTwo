using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves all direct replies to a specific comment.
/// Validates that the parent comment exists before fetching replies.
/// Replies use the self-referencing ParentCommentId relationship for flat display
/// with a "replying to @username" reference.
/// </summary>
public record GetRepliesQuery(int CommentId) : IRequest<Result<IReadOnlyList<CommentDto>>>;

/// <summary>
/// Handler for GetRepliesQuery.
/// Verifies the parent comment exists, then fetches all direct replies and maps them to DTOs.
/// </summary>
public class GetRepliesHandler : IRequestHandler<GetRepliesQuery, Result<IReadOnlyList<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;

    public GetRepliesHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<IReadOnlyList<CommentDto>>> Handle(GetRepliesQuery request, CancellationToken cancellationToken)
    {
        if (!await _commentRepository.ExistsAsync(c => c.CommentId == request.CommentId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CommentDto>>("Comment not found.");
        }
        
        var replies = await _commentRepository.GetRepliesAsync(request.CommentId, cancellationToken);

        // Map each reply to a CommentDto (includes soft-delete display logic)
        var dtos = replies.Select(c => c.ToCommentDto()).ToList();

        return Result.Success<IReadOnlyList<CommentDto>>(dtos);
    }
}

