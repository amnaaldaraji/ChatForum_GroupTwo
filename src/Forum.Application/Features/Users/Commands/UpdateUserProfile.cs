using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Commands;

/// <summary>
/// Command to update a user's profile information (username and/or email).
/// Supports authorization: only the user themselves or an admin can perform this update.
/// </summary>
public record UpdateUserProfileCommand(
    string UserId,
    string? UserName,
    string? Email,
    string RequestingUserId,
    bool IsAdmin) : IRequest<Result<UserDto>>;

/// <summary>
/// Handles the UpdateUserProfileCommand by validating authorization,
/// checking uniqueness constraints, updating the user entity, and persisting changes.
/// </summary>
public class UpdateUserProfileHandler : IRequestHandler<UpdateUserProfileCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserProfileHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command to update a user's profile.
    /// </summary>
    public async Task<Result<UserDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId != request.RequestingUserId && !request.IsAdmin)
        {
            return Result.Failure<UserDto>("You are not authorized to update this profile.");
        }
        
        var user = await _userRepository.GetByIdWithDetailsAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<UserDto>("User not found.");
        }
        
        if (user.IsDeleted)
        {
            return Result.Failure<UserDto>("Cannot update a deleted user.");
        }
        
        if (request.UserName != null)
        {
            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                return Result.Failure<UserDto>("Username cannot be empty.");
            }
            
            if (await _userRepository.UserNameExistsAsync(request.UserName, request.UserId, cancellationToken))
            {
                return Result.Failure<UserDto>("Username is already taken.");
            }
            
            user.UserName = request.UserName.Trim();
            user.NormalizedUserName = request.UserName.Trim().ToUpperInvariant();
        }
        
        if (request.Email != null)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Result.Failure<UserDto>("Email cannot be empty.");
            }
            
            if (await _userRepository.EmailExistsAsync(request.Email, request.UserId, cancellationToken))
            {
                return Result.Failure<UserDto>("Email is already in use.");
            }
            
            user.Email = request.Email.Trim();
            user.NormalizedEmail = request.Email.Trim().ToUpperInvariant();
        }
        
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success(user.ToUserDto());
    }
}

