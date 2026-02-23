using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Commands;

/// <summary>
/// Command to soft-delete a user account. The user's IsDeleted flag is set to true,
/// causing their display name to appear as "Deleted User" throughout the forum.
/// Only the user themselves or an admin can perform this action.
/// </summary>
public record DeleteUserCommand(string UserId, string RequestingUserId, bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handles the DeleteUserCommand by performing a soft-delete on the user.
/// The user entity is not removed from the database; instead, the IsDeleted flag is set to true,
/// preserving referential integrity for existing threads, comments, and replies.
/// </summary>
public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command to soft-delete a user.
    /// </summary>
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId != request.RequestingUserId && !request.IsAdmin)
        {
            return Result.Failure("You are not authorized to delete this user.", ErrorType.Forbidden);
        }
        
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure("User not found.", ErrorType.NotFound);
        }
        
        if (user.IsDeleted)
        {
            return Result.Failure("User is already deleted.", ErrorType.Conflict);
        }
        
        user.IsDeleted = true;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}

