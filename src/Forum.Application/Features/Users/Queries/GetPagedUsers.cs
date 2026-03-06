using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Queries;

/// <summary>
/// Retrieves a paginated list of users with their thread and comment counts.
/// Used by the admin panel to list and manage user accounts.
/// </summary>
public record GetPagedUsersQuery(int PageNumber = 1, int PageSize = 10) : IRequest<Result<PagedResult<UserDto>>>;

public class GetPagedUsersHandler : IRequestHandler<GetPagedUsersQuery, Result<PagedResult<UserDto>>>
{
    private readonly IUserRepository _userRepository;

    public GetPagedUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<PagedResult<UserDto>>> Handle(GetPagedUsersQuery request, CancellationToken cancellationToken)
    {
        var paginationParams = new PaginationParams
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        var (users, totalCount) = await _userRepository.GetPagedAsync(paginationParams, cancellationToken);

        var dtos = users.Select(u => u.ToUserDto()).ToList();

        return Result.Success(PagedResult<UserDto>.Create(
            dtos,
            totalCount,
            paginationParams.PageNumber,
            paginationParams.PageSize
        ));
    }
}
