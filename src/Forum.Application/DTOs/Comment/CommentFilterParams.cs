using Forum.Application.Common.Models;

namespace Forum.Application.DTOs.Comment;

/// <summary>
/// Filter parameters for paginated comment queries. Extends PaginationParams
/// to inherit PageNumber and PageSize, then adds comment-specific filters:
/// thread, author, date range, and sort order.
///
/// Used by the GetComments CQRS query for fetching filtered comment lists.
/// </summary>
public class CommentFilterParams : PaginationParams
{
    public int? ThreadId { get; set; }
    public string? AuthorId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public CommentSortBy SortBy { get; set; } = CommentSortBy.Newest;
}

public enum CommentSortBy
{
    Newest,
    Oldest
}