using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Commands;

/// <summary>
/// Updates the content of an existing comment.
/// Only the comment owner or an admin can perform this action. Soft-deleted comments cannot be updated.
/// </summary>
public record UpdateCommentCommand(int CommentId, string Content, string UserId, bool IsAdmin) : IRequest<Result<CommentDto>>;

/// <summary>
/// Handler for UpdateCommentCommand. Validates the comment exists, is not
/// soft-deleted, the user is authorized, and the new content is valid before applying the update.
/// </summary>
public class UpdateCommentHandler : IRequestHandler<UpdateCommentCommand, Result<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCommentHandler(ICommentRepository commentRepository, IUnitOfWork unitOfWork)
    {
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CommentDto>> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _commentRepository.GetByIdWithDetailsAsync(request.CommentId, cancellationToken);
        if (comment == null)
        {
            return Result.Failure<CommentDto>("Comment not found.", ErrorType.NotFound);
        }
        
        if (comment.IsDeleted)
        {
            return Result.Failure<CommentDto>("Cannot update a deleted comment.", ErrorType.Conflict);
        }
        
        if (comment.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure<CommentDto>("You are not authorized to update this comment.", ErrorType.Forbidden);
        }
        
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result.Failure<CommentDto>("Comment content is required.");
        }
        
        comment.Content = request.Content;
        _commentRepository.Update(comment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(comment.ToCommentDto());
    }
}