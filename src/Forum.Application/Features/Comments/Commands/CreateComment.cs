using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;

namespace Forum.Application.Features.Comments.Commands;

/// <summary>
/// Creates a new comment on a thread, optionally as a reply to another comment.
/// Validates that the thread exists, content is not empty, and (if replying) the parent comment
/// exists and belongs to the same thread. Also updates the thread's TimeUpdated timestamp.
/// </summary>
public record CreateCommentCommand(string UserId, int ThreadId, string Content, int? ParentCommentId = null) : IRequest<Result<CommentDto>>;

/// <summary>
/// Handler for CreateCommentCommand.
/// </summary>
public class CreateCommentHandler : IRequestHandler<CreateCommentCommand, Result<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCommentHandler(
        ICommentRepository commentRepository,
        IThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _commentRepository = commentRepository;
        _threadRepository = threadRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CommentDto>> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result.Failure<CommentDto>("Comment content is required.");
        }
        
        var thread = await _threadRepository.GetByIdAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure<CommentDto>("Thread not found.");
        }

        // If replying to another comment, validate the parent comment exists and is in the same thread
        if (request.ParentCommentId.HasValue)
        {
            var parentComment = await _commentRepository.GetByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment == null)
            {
                return Result.Failure<CommentDto>("Parent comment not found.");
            }

            // Prevent cross-thread replies — parent comment must belong to the same thread
            if (parentComment.ThreadId != request.ThreadId)
            {
                return Result.Failure<CommentDto>("Parent comment must belong to the same thread.");
            }
        }
        
        var comment = new Comment
        {
            Content = request.Content,
            ThreadId = request.ThreadId,
            UserId = request.UserId,
            ParentCommentId = request.ParentCommentId,
            TimeCreated = DateTime.UtcNow
        };
        
        await _commentRepository.AddAsync(comment, cancellationToken);
        
        thread.TimeUpdated = DateTime.UtcNow;
        _threadRepository.Update(thread);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        var createdComment = await _commentRepository.GetByIdWithDetailsAsync(comment.CommentId, cancellationToken);
        return Result.Success(createdComment!.ToCommentDto());
    }
}