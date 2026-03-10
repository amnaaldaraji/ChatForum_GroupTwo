using Forum.Application.Common.Models;

namespace Forum.Application.DTOs.User;

/// <summary>
/// Pagination and sorting parameters for the admin user listing endpoint.
/// </summary>
public class UserFilterParams : PaginationParams
{
    public UserSortBy SortBy { get; set; } = UserSortBy.Username;
}

/// <summary>
/// Available sort options for the user listing.
/// </summary>
public enum UserSortBy
{
    Username,
    Threads,
    Comments
}
