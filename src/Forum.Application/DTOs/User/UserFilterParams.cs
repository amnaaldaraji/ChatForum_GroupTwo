using Forum.Application.Common.Models;

namespace Forum.Application.DTOs.User;

public class UserFilterParams : PaginationParams
{
    public UserSortBy SortBy { get; set; } = UserSortBy.Username;
}

public enum UserSortBy
{
    Username,
    Threads,
    Comments
}
