using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Queries;

/// <summary>
/// Retrieves all users with their thread and comment counts.
/// Used by the admin panel to list and manage user accounts.
/// </summary>
public record GetAllUsers : IRequest<Result<List<UserDto>>>;

/// <summary>
/// Handler for GetAllUsers.
/// Fetches all users from the repository (with threads and comments eagerly loaded) and maps them to DTOs.
/// Soft-deleted users are included so admins can see their status.
/// </summary>
public class GetAllUsersHandler : IRequestHandler<GetAllUsers, Result<List<UserDto>>>
{
    private readonly IUserRepository _userRepository;

    public GetAllUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Handles the query by fetching all users from the database,
    /// mapping each entity to a UserDto, and wrapping the result in a success Result.
    /// </summary>
    public async Task<Result<List<UserDto>>> Handle(GetAllUsers request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        
        var dtos = users.Select(u => u.ToUserDto()).ToList();

        return Result.Success(dtos);
    }
}
