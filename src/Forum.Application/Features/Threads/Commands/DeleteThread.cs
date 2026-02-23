using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Commands;

/// <summary>
/// Deletes a thread (hard delete).
/// All associated comments are removed via cascade delete.
/// Authorization: only the thread owner or an admin can delete.
/// </summary>
public record DeleteThreadCommand(int ThreadId, string UserId, bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handler for DeleteThreadCommand.
/// Validates the thread exists, checks authorization, and performs a hard delete with cascade.
/// </summary>
public class DeleteThreadHandler : IRequestHandler<DeleteThreadCommand, Result>
{
    private readonly IThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteThreadHandler(IThreadRepository threadRepository, IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result> Handle(DeleteThreadCommand request, CancellationToken cancellationToken)
    {
        var thread = await _threadRepository.GetByIdWithDetailsAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure("Thread not found.", ErrorType.NotFound);
        }
        
        if (thread.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure("You are not authorized to delete this thread.", ErrorType.Forbidden);
        }

        // Hard delete — EF Core cascade will remove all associated comments
        _threadRepository.Delete(thread);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}