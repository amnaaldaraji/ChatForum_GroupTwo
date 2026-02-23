using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves a single comment by its ID, including related details
/// (user info, parent comment reference). Returns a failure if the comment does not exist.
/// </summary>
public record GetCommentByIdQuery(int CommentId) : IRequest<Result<CommentDto>>;

/// <summary>
/// Handler for GetCommentByIdQuery.
/// Fetches a comment with its related data (User, ParentComment) and maps it to a DTO.
/// </summary>
public class GetCommentByIdHandler : IRequestHandler<GetCommentByIdQuery, Result<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;

    public GetCommentByIdHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query by fetching the comment with its details (user, parent comment)
    /// from the repository and mapping it to a CommentDto.
    /// </summary>
    public async Task<Result<CommentDto>> Handle(GetCommentByIdQuery request, CancellationToken cancellationToken)
    {
        // Fetch the comment with eagerly loaded User and ParentComment navigation properties
        var comment = await _commentRepository.GetByIdWithDetailsAsync(request.CommentId, cancellationToken);
        if (comment == null)
        {
            return Result.Failure<CommentDto>("Comment not found.", ErrorType.NotFound);
        }

        // Map to DTO (soft-delete display logic applied in mapping: "[deleted]" content, "Deleted User" username)
        return Result.Success(comment.ToCommentDto());
    }
}