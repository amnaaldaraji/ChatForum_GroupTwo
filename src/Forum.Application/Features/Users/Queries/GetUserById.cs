using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Queries;

/// <summary>
/// Query to retrieve a single user by their unique identifier.
/// </summary>
public record GetUserByIdQuery(string UserId) : IRequest<Result<UserDto>>;

/// <summary>
/// Handles the GetUserByIdQuery by looking up the user in the repository
/// and mapping the result to a UserDto.
/// </summary>
public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Handles the query to retrieve a user by their ID.
    /// </summary>
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithDetailsAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure<UserDto>("User not found.", ErrorType.NotFound);
        }
        
        return Result.Success(user.ToUserDto());
    }
}

