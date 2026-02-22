using System.Web;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// API service for thread operations. Calls Forum.Api thread endpoints.
/// </summary>
public class ThreadService : ApiClientBase
{
    public ThreadService(IHttpClientFactory httpClientFactory, ITokenStorageService tokenStorage)
        : base(httpClientFactory, tokenStorage)
    {
    }

    /// <summary>
    /// GET /api/threads?categoryId=...&searchTerm=...&sortBy=...&pageNumber=...&pageSize=...
    /// Returns a PagedResult containing ThreadSummaryDto items and pagination metadata.
    /// </summary>
    public async Task<PagedResult<ThreadSummaryDto>?> GetThreadsAsync(ThreadFilterParams? filter = null)
    {
        var query = BuildQueryString(filter);
        return await GetAsync<PagedResult<ThreadSummaryDto>>($"api/threads{query}");
    }

    /// <summary>
    /// GET /api/threads/{id} — returns full thread details including body comment and all comments.
    /// </summary>
    public async Task<ThreadDetailDto?> GetByIdAsync(int id)
    {
        return await GetAsync<ThreadDetailDto>($"api/threads/{id}");
    }

    /// <summary>
    /// POST /api/threads — creates a new thread with a body (stored as the first comment).
    /// </summary>
    public async Task<HttpResponseMessage> CreateAsync(CreateThreadDto dto)
    {
        return await PostAsync("api/threads", dto);
    }

    /// <summary>
    /// PUT /api/threads/{id} — updates a thread's title or category.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(int id, UpdateThreadDto dto)
    {
        return await PutAsync($"api/threads/{id}", dto);
    }

    /// <summary>
    /// DELETE /api/threads/{id} — deletes a thread (hard delete).
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(int id)
    {
        return await DeleteAsync($"api/threads/{id}");
    }

    /// <summary>
    /// Converts a ThreadFilterParams object into a URL query string.
    /// Example output: "?categoryId=1&sortBy=Newest&pageNumber=1&pageSize=20"
    /// </summary>
    private static string BuildQueryString(ThreadFilterParams? filter)
    {
        if (filter == null) return string.Empty;

        var queryParams = HttpUtility.ParseQueryString(string.Empty);

        if (filter.CategoryId.HasValue)
            queryParams["categoryId"] = filter.CategoryId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            queryParams["searchTerm"] = filter.SearchTerm;
        if (!string.IsNullOrWhiteSpace(filter.AuthorId))
            queryParams["authorId"] = filter.AuthorId;
        if (filter.FromDate.HasValue)
            queryParams["fromDate"] = filter.FromDate.Value.ToString("o");
        if (filter.ToDate.HasValue)
            queryParams["toDate"] = filter.ToDate.Value.ToString("o");
        queryParams["sortBy"] = filter.SortBy.ToString();

        queryParams["pageNumber"] = filter.PageNumber.ToString();
        queryParams["pageSize"] = filter.PageSize.ToString();

        var qs = queryParams.ToString();
        return string.IsNullOrEmpty(qs) ? string.Empty : $"?{qs}";
    }
}
