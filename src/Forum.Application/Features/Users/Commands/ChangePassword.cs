using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using MediatR;

namespace Forum.Application.Features.Users.Commands;

/// <summary>
/// Changes a user's password after verifying the current password.
/// Owners can change their own; admins can change any user's password.
/// </summary>
public record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword,
    string RequestingUserId,
    bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handles <see cref="ChangePasswordCommand"/> — authorisation check then delegates to <see cref="IAuthService"/>.
/// </summary>
public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IAuthService _authService;

    public ChangePasswordHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId != request.RequestingUserId && !request.IsAdmin)
            return Result.Failure("You are not authorized to change this password.", ErrorType.Forbidden);

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return Result.Failure("New password cannot be empty.");

        return await _authService.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword);
    }
}
