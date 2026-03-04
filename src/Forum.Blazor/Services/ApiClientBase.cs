using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// Abstract base class for all API service classes (AuthService, ThreadService, etc).
/// Each typed service inherits from this and calls these protected methods to communicate with Forum.Api endpoints.
/// </summary>
public abstract class ApiClientBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorageService _tokenStorage;

    /// <summary>
    /// Shared JSON options.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected ApiClientBase(IHttpClientFactory httpClientFactory, ITokenStorageService tokenStorage)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
    }

    /// <summary>
    /// Creates an HttpClient from the "ForumApi" named client.
    /// When authenticate is true, attaches the stored JWT as a Bearer token in the Authorization header.
    /// </summary>
    protected async Task<HttpClient> CreateClientAsync(bool authenticate = true)
    {
        var client = _httpClientFactory.CreateClient("ForumApi");

        if (authenticate)
        {
            var token = await _tokenStorage.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    /// <summary>
    /// Sends an unauthenticated GET request.
    /// Used for public endpoints (thread listings, categories).
    /// </summary>
    protected async Task<T?> GetAsync<T>(string url)
    {
        var client = await CreateClientAsync(authenticate: false);
        return await client.GetFromJsonAsync<T>(url, JsonOptions);
    }

    /// <summary>
    /// Sends a GET request with the JWT Bearer token attached.
    /// Used for endpoints that require authentication (like user profile when viewing own data).
    /// </summary>
    protected async Task<T?> GetAuthenticatedAsync<T>(string url)
    {
        var client = await CreateClientAsync();
        return await client.GetFromJsonAsync<T>(url, JsonOptions);
    }

    /// <summary>
    /// Sends an authenticated POST request with a JSON body.
    /// Used for creating resources (threads, comments, etc).
    /// </summary>
    protected async Task<HttpResponseMessage> PostAsync<T>(string url, T data)
    {
        var client = await CreateClientAsync();
        return await client.PostAsJsonAsync(url, data, JsonOptions);
    }

    /// <summary>
    /// Sends an authenticated PUT request with a JSON body.
    /// Used for updating resources (thread title, comment content, etc).
    /// </summary>
    protected async Task<HttpResponseMessage> PutAsync<T>(string url, T data)
    {
        var client = await CreateClientAsync();
        return await client.PutAsJsonAsync(url, data, JsonOptions);
    }

    /// <summary>
    /// Sends an authenticated DELETE request.
    /// Used for deleting resources (threads, comments, etc).
    /// </summary>
    protected async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        var client = await CreateClientAsync();
        return await client.DeleteAsync(url);
    }

    /// <summary>
    /// Attempts to extract a human-readable error message from an API ProblemDetails JSON response.
    /// Returns null if the body cannot be parsed or contains neither "detail" nor "title".
    /// </summary>
    public static string? TryExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
                return detail.GetString();
            if (doc.RootElement.TryGetProperty("title", out var title))
                return title.GetString();
        }
        catch { }
        return null;
    }
}
