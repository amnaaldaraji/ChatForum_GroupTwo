using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Commands;

/// <summary>
/// Soft-deletes a comment (sets IsDeleted = true).
/// Only the comment owner or an admin can perform this action.
/// The first comment in a thread (which serves as the thread body) cannot be deleted
/// — the entire thread must be deleted in that case.
/// Soft-deleted comments display as "[deleted]" with replies preserved.
/// </summary>
public record DeleteCommentCommand(int CommentId, string UserId, bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handler for DeleteCommentCommand.
/// Validates the comment exists, is not already deleted, the user is authorized,
/// and the comment is not the thread body (first comment), before performing the soft-delete.
/// </summary>
public class DeleteCommentHandler : IRequestHandler<DeleteCommentCommand, Result>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCommentHandler(ICommentRepository commentRepository, IUnitOfWork unitOfWork)
    {
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _commentRepository.GetByIdWithDetailsAsync(request.CommentId, cancellationToken);
        if (comment == null)
        {
            return Result.Failure("Comment not found.", ErrorType.NotFound);
        }
        
        if (comment.IsDeleted)
        {
            return Result.Failure("Comment is already deleted.", ErrorType.Conflict);
        }
        
        if (comment.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure("You are not authorized to delete this comment.", ErrorType.Forbidden);
        }

        // The first comment in a thread serves as the thread body and cannot be deleted independently. 
        var firstComment = await _commentRepository.GetFirstCommentByThreadIdAsync(comment.ThreadId, cancellationToken);
        if (firstComment != null && firstComment.CommentId == request.CommentId)
        {
            return Result.Failure("Cannot delete the thread body comment. Delete the thread instead.", ErrorType.Conflict);
        }

        // Soft-delete: set IsDeleted flag (content will display as "[deleted]", replies are preserved)
        comment.IsDeleted = true;
        _commentRepository.Update(comment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}