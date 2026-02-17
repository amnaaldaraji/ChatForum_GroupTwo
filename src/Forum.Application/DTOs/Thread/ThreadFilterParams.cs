using Forum.Application.Common.Models;

namespace Forum.Application.DTOs.Thread;

/// <summary>
/// Filter and sort parameters for paginated thread queries. Extends PaginationParams
/// to inherit PageNumber and PageSize, then adds thread-specific filters:
/// category, author, search text, date range, and sort order.
///
/// Used by the GetThreads CQRS query and the thread listing API endpoint.
/// </summary>
public class ThreadFilterParams : PaginationParams
{
    public int? CategoryId { get; set; }
    public string? AuthorId { get; set; }
    public string? SearchTerm { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public ThreadSortBy SortBy { get; set; } = ThreadSortBy.Newest;
}

public enum ThreadSortBy
{
    Newest,
    Oldest,
    MostComments,
    RecentlyUpdated
}
