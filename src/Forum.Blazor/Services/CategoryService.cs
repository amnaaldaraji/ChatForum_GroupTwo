using Forum.Application.DTOs.Category;

namespace Forum.Blazor.Services;

/// <summary>
/// API service for category operations. Calls Forum.Api category endpoints.
/// </summary>
public class CategoryService : ApiClientBase
{
    public CategoryService(IHttpClientFactory httpClientFactory, TokenStorageService tokenStorage)
        : base(httpClientFactory, tokenStorage)
    {
    }

    /// <summary>
    /// GET /api/categories — returns all categories with their thread counts.
    /// </summary>
    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await GetAsync<List<CategoryDto>>("api/categories") ?? [];
    }

    /// <summary>
    /// GET /api/categories/{id} — returns a single category by ID.
    /// </summary>
    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        return await GetAsync<CategoryDto>($"api/categories/{id}");
    }

    /// <summary>
    /// POST /api/categories — creates a new category. Requires admin role.
    /// </summary>
    public async Task<HttpResponseMessage> CreateAsync(CreateCategoryDto dto)
    {
        return await PostAsync("api/categories", dto);
    }

    /// <summary>
    /// PUT /api/categories/{id} — updates a category's name. Requires admin role.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(int id, UpdateCategoryDto dto)
    {
        return await PutAsync($"api/categories/{id}", dto);
    }

    /// <summary>
    /// DELETE /api/categories/{id} — deletes a category (hard delete). Requires admin role.
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(int id)
    {
        return await DeleteAsync($"api/categories/{id}");
    }
}
