namespace Forum.Application.Common.Models;

/// <summary>
/// Extended by entity-specific filter classes (like ThreadFilterParams etc). 
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 20;
    private const int DefaultPageSize = 10;
    private int _pageNumber = 1;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber; 
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize; 
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? DefaultPageSize : value;
    }
}