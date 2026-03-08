using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// API service for comment operations. Calls Forum.Api comment endpoints.
/// </summary>
public class CommentService : ApiClientBase
{
    public CommentService(IHttpClientFactory httpClientFactory, ITokenStorageService tokenStorage)
        : base(httpClientFactory, tokenStorage)
    {
    }

    /// <summary>
    /// GET /api/comments/thread/{threadId}?pageNumber=...&pageSize=...
    /// Returns paginated comments for a thread, ordered by creation time.
    /// Default: 20 comments per page.
    /// </summary>
    public async Task<PagedResult<CommentDto>?> GetByThreadAsync(int threadId, int pageNumber = 1, int pageSize = 20)
    {
        return await GetAsync<PagedResult<CommentDto>>(
            $"api/comments/thread/{threadId}?pageNumber={pageNumber}&pageSize={pageSize}");
    }

    /// <summary>
    /// GET /api/comments?authorId=...&pageNumber=...&pageSize=...
    /// Returns paginated comments filtered by author.
    /// </summary>
    public async Task<PagedResult<CommentDto>?> GetByAuthorAsync(string authorId, int pageNumber = 1, int pageSize = 20)
    {
        return await GetAsync<PagedResult<CommentDto>>(
            $"api/comments?authorId={authorId}&pageNumber={pageNumber}&pageSize={pageSize}");
    }

    /// <summary>
    /// GET /api/comments/{id} — returns a single comment by ID.
    /// </summary>
    public async Task<CommentDto?> GetByIdAsync(int id)
    {
        return await GetAsync<CommentDto>($"api/comments/{id}");
    }

    /// <summary>
    /// POST /api/comments/{threadId} — creates a new comment in the given thread.
    /// </summary>
    public async Task<HttpResponseMessage> CreateAsync(int threadId, CreateCommentDto dto)
    {
        return await PostAsync($"api/comments/{threadId}", dto);
    }

    /// <summary>
    /// PUT /api/comments/{id} — updates a comment's content.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(int id, UpdateCommentDto dto)
    {
        return await PutAsync($"api/comments/{id}", dto);
    }

    /// <summary>
    /// DELETE /api/comments/{id} — soft-deletes a comment (content becomes "[deleted]").
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(int id)
    {
        return await base.DeleteAsync($"api/comments/{id}");
    }
}
