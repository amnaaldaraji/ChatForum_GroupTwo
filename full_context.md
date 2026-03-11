## File: \src\Forum.Api\Endpoints\AuthEndpoints.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.DTOs.Auth;
using Forum.Application.Features.Auth.Commands;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Defines the authentication-related minimal API endpoints for user registration, login, and logout.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Maps all authentication endpoints to the application's endpoint route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // POST /api/auth/register
        // Dispatches a RegisterUserCommand via MediatR to create a new user account.
        group.MapPost("/register", async (RegisterRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RegisterUserCommand(request.Username, request.Email, request.Password), ct);
            if (result.IsSuccess)
                return Results.Ok(result.Value);

            var errors = result.Error!.Split("; ");
            return Results.Problem(
                title: "Bad Request",
                detail: result.Error,
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["errors"] = errors });
        }).AllowAnonymous();

        // POST /api/auth/login
        // Dispatches a LoginUserCommand via MediatR to validate credentials and generate a JWT.
        group.MapPost("/login", async (LoginRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new LoginUserCommand(request.Username, request.Password), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Problem(
                    title: "Unauthorized",
                    detail: result.Error,
                    statusCode: StatusCodes.Status401Unauthorized);
        }).AllowAnonymous();

        // POST /api/auth/logout
        // Calls the IAuthService directly to perform server-side logout (sign-out from Identity).
        group.MapPost("/logout", async (IAuthService authService) =>
        {
            var result = await authService.LogoutAsync();
            return Results.Ok(new { Message = "Logged out successfully" });
        }).RequireAuthorization();

        return app;
    }
}

```
## File: \src\Forum.Api\Endpoints\CategoryEndpoints.cs
```cs
using Forum.Api.Extensions;
using Forum.Application.DTOs.Category;
using Forum.Application.Features.Categories.Commands;
using Forum.Application.Features.Categories.Queries;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for CRUD operations on forum categories (admin-only for write operations).
/// </summary>
public static class CategoryEndpoints
{
    /// <summary> Registers category endpoints under /api/categories </summary>
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories");


        // GET all categories
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllCategoriesQuery(), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .AllowAnonymous();

        // GET a single category by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCategoryByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        // Added route name so other endpoints (like POST)
        // can reference this route safely using CreatedAtRoute.
        // This avoids hardcoded URL strings.
        .WithName("GetCategoryById")
        .AllowAnonymous();

        // POST a new category to create it (Admin only)
        group.MapPost("/", async (CreateCategoryDto dto, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CreateCategoryCommand(dto.Name), ct);
            return result.IsSuccess
                ? Results.Created($"/api/categories/{result.Value!.CategoryId}", result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // PUT to update a category name (Admin only)
        group.MapPut("/{id:int}", async (int id, UpdateCategoryDto dto, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new UpdateCategoryCommand(id, dto.Name), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // DELETE a category (Admin only)
        group.MapDelete("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteCategoryCommand(id), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        return app;
    }
}
```
## File: \src\Forum.Api\Endpoints\CommentEndpoints.cs
```cs
using Forum.Application.Features.Comments.Commands;
using Forum.Application.Features.Comments.Queries;
using Forum.Api.Extensions;
using Forum.Application.DTOs.Comment;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for CRUD operations on comments, including per-thread listing with vote scores.
/// </summary>
public static class CommentEndpoints
{
    /// <summary> Registers comment endpoints under /api/comments. </summary>
    public static IEndpointRouteBuilder MapCommentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/comments")
            .WithTags("Comments");

        // GET paginated comments (filterable by author, thread, date)
        group.MapGet("/", async ([AsParameters] CommentFilterParams filterParams, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCommentsQuery(filterParams), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetComments")
        .AllowAnonymous();

        // GET comment by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCommentByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetCommentById")
        .AllowAnonymous();

        // GET paged comments for thread
        // Pass current userId so vote data is included per user — stays AllowAnonymous,
        // userId is simply null for unauthenticated requests
        group.MapGet("/thread/{threadId:int}", async (
            int threadId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct,
            int pageNumber = 1,
            int pageSize = 50) =>
        {
            var currentUserId = httpContext.User.GetUserId();
            var result = await mediator.Send(
                new GetCommentsByThreadQuery(threadId, pageNumber, pageSize, currentUserId), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .AllowAnonymous();

        // POST to create a new comment
        group.MapPost("/{threadId:int}", async (int threadId, CreateCommentDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new CreateCommentCommand(userId, threadId, dto.Content, dto.ParentCommentId), ct);
            return result.IsSuccess
                ? Results.Created($"/api/comments/{result.Value!.CommentId}", result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // PUT to update a comment
        group.MapPut("/{id:int}", async (int id, UpdateCommentDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new UpdateCommentCommand(id, dto.Content, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // DELETE to remove a comment
        group.MapDelete("/{id:int}", async (int id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new DeleteCommentCommand(id, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization();

        return app;
    }
}```
## File: \src\Forum.Api\Endpoints\ThreadEndpoints.cs
```cs
using Forum.Application.DTOs.Thread;
using Forum.Application.Features.Threads.Commands;
using Forum.Application.Features.Threads.Queries;
using Forum.Api.Extensions;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for CRUD operations on discussion threads.
/// </summary>
public static class ThreadEndpoints
{
    /// <summary> Registers thread endpoints under /api/threads. </summary>
    public static IEndpointRouteBuilder MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/threads")
            .WithTags("Threads");

        // GET a paged list of threads
        group.MapGet("/", async ([AsParameters] ThreadFilterParams filter, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadsQuery(filter), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .AllowAnonymous();

        // GET a thread by id
        group.MapGet("/{id:int}", async (int id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetThreadByIdQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        .WithName("GetThreadById")
        .AllowAnonymous();

        // POST to create a new thread
        group.MapPost("/", async (CreateThreadDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new CreateThreadCommand(userId, dto.Title, dto.CategoryId, dto.Body), ct);
            return result.IsSuccess
                ? Results.Created($"/api/threads/{result.Value!.ThreadId}", result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // PUT to update a thread
        group.MapPut("/{id:int}", async (int id, UpdateThreadDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new UpdateThreadCommand(id, dto.Title, dto.CategoryId, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // DELETE to remove a thread
        group.MapDelete("/{id:int}", async (int id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var isAdmin = httpContext.User.IsAdmin();
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var result = await mediator.Send(new DeleteThreadCommand(id, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization();

        return app;
    }
}```
## File: \src\Forum.Api\Endpoints\UserEndpoints.cs
```cs
using Forum.Api.Extensions;
using Forum.Application.DTOs.User;
using Forum.Application.Features.Users.Commands;
using Forum.Application.Features.Users.Queries;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for user profiles, password changes, and account management.
/// </summary>
public static class UserEndpoints
{
    /// <summary> Registers user endpoints under /api/users. </summary>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        // GET paged users (Admin only)
        group.MapGet("/", async (IMediator mediator, CancellationToken ct, int pageNumber = 1, int pageSize = 10, string? sortBy = null) =>
        {
            var sort = Enum.TryParse<Application.DTOs.User.UserSortBy>(sortBy, true, out var parsed)
                ? parsed
                : Application.DTOs.User.UserSortBy.Username;
            var result = await mediator.Send(new GetPagedUsersQuery(pageNumber, pageSize, sort), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization(p => p.RequireRole("Admin"));

        // GET a user profile by id
        group.MapGet("/{id}", async (string id, IMediator mediator, CancellationToken ct) =>
        {

            var result = await mediator.Send(new GetUserProfileQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        })
        // Giving this endpoint a stable name for its routing to make it simpler to reference.
        .WithName("GetUserProfileById")
        .AllowAnonymous();

        // PUT to update a user profile, only allowed for the owner or an Admin
        group.MapPut("/{id}", async (string id, UpdateUserProfileDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }
            
            var isAdmin = httpContext.User.IsAdmin();
            
            if (userId != id && !isAdmin)
            {
                return Results.Forbid();
            }

            var result = await mediator.Send(new UpdateUserProfileCommand(id, dto.UserName, dto.Email, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // POST to change password for a user, only allowed for the owner or an Admin
        group.MapPost("/{id}/change-password", async (string id, ChangePasswordDto dto, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var isAdmin = httpContext.User.IsAdmin();
            if (userId != id && !isAdmin)
                return Results.Forbid();

            var result = await mediator.Send(new ChangePasswordCommand(id, dto.CurrentPassword, dto.NewPassword, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.Ok(new { Message = "Password changed successfully." })
                : result.ToProblemDetails();
        }).RequireAuthorization();

        // DELETE a user account (soft delete), only for the owner or an Admin
        group.MapDelete("/{id}", async (string id, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }
            
            var isAdmin = httpContext.User.IsAdmin();
            
            if (userId != id && !isAdmin)
            {
                return Results.Forbid();
            }

            var result = await mediator.Send(new DeleteUserCommand(id, userId, isAdmin), ct);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        }).RequireAuthorization();

        return app;
    }
}```
## File: \src\Forum.Api\Endpoints\VoteEndpoints.cs
```cs
using Forum.Api.Extensions;
using Forum.Application.DTOs.Vote;
using Forum.Application.Features.Votes.Commands;
using MediatR;

namespace Forum.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for casting upvotes/downvotes on comments.
/// </summary>
public static class VoteEndpoints
{
    /// <summary>
    /// Registers vote-related endpoints under /api/comments/{commentId}/votes.
    /// </summary>
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        // POST to cast a vote on a comment
        app.MapPost("/api/comments/{commentId:int}/votes", async (
                int commentId,
                CastVoteDto dto,
                HttpContext httpContext,
                IMediator mediator,
                CancellationToken ct) =>
            {
                // Extract the authenticated user's ID from the JWT/cookie claims
                var userId = httpContext.User.GetUserId();

                // Reject the request if the user identity could not be resolved
                if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

                // Dispatch the vote command through MediatR to the application layer
                var result = await mediator.Send(new CastVoteCommand(userId, commentId, dto.Value), ct);

                // Return 200 OK with the updated vote state, or a structured problem response on failure
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.ToProblemDetails();
            })
            .RequireAuthorization() // Endpoint requires an authenticated user
            .WithTags("Votes");     // Groups the endpoint under "Votes" in Swagger/OpenAPI

        return app;
    }
}```
## File: \src\Forum.Api\Extensions\ClaimsPrincipalExtensions.cs
```cs
using System.Security.Claims;

namespace Forum.Api.Extensions;

/// <summary>
/// Provides extension methods to simplify extraction of user identity information
/// from JWT claims in minimal API endpoints.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Extracts the authenticated user's unique identifier from the JWT token claims.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Determines whether the authenticated user has the "Admin" role.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal user)
        => user.IsInRole("Admin");
}```
## File: \src\Forum.Api\Extensions\ProblemDetailsMapping.cs
```cs
using Forum.Application.Common.Models;

namespace Forum.Api.Extensions;

/// <summary>
/// Extension methods for converting Result failures into Problem Details responses.
/// </summary>
public static class ProblemDetailsMapping
{
    /// <summary>
    /// Maps a failed Result to an IResult with the appropriate HTTP status code.
    /// </summary>
    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Cannot convert a successful result to a problem.");

        var (statusCode, title) = result.ErrorType switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Bad Request")
        };

        return Results.Problem(
            title: title,
            detail: result.Error,
            statusCode: statusCode);
    }
}
```
## File: \src\Forum.Api\Program.cs
```cs
using System.Text;
using Forum.Api.Endpoints;
using Forum.Application;
using Forum.Domain.Entities;
using Forum.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// Register Application layer services (MediatR handlers, business logic services).
builder.Services.AddApplicationServices();

// Resolve the database path to the solution root so it works regardless of working directory.
var solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
var dbPath = Path.Combine(solutionRoot, "forum.db");
builder.Configuration["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath}";

// Register Infrastructure layer services (DbContext, repositories, UnitOfWork, AuthService).
builder.Services.AddInfrastructureServices(builder.Configuration);

// Configure Identity with the custom User entity (extends IdentityUser) and IdentityRole.
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<Forum.Infrastructure.Data.ForumDbContext>()
    .AddDefaultTokenProviders();

// Read JWT settings from configuration, falling back to development defaults.
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGeneration123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ForumApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ForumClient";

// Set JWT Bearer as the default authentication.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Register the authorization services required by RequireAuthorization() on endpoints.
builder.Services.AddAuthorization();

// Allow the Blazor Server frontend to make cross-origin requests to this API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("http://localhost:5159", "https://localhost:7224")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Apply migrations and seed the database.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<Forum.Infrastructure.Data.ForumDbContext>();
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Apply any pending migrations â€” also creates the DB file if it doesn't exist.
        context.Database.Migrate();

        // Seed roles, users, categories, threads and comments.
        Forum.Infrastructure.Data.DbSeeder.SeedAsync(context, userManager, roleManager).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        // Log seeding errors but do not crash the application.
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors("AllowBlazor");
app.UseAuthentication();
app.UseAuthorization();

app.MapCategoryEndpoints();
app.MapThreadEndpoints();
app.MapCommentEndpoints();
app.MapUserEndpoints();
app.MapAuthEndpoints();
app.MapVoteEndpoints();

app.Run();
```
## File: \src\Forum.Application\Common\Interfaces\IAuthService.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Auth;

namespace Forum.Application.Common.Interfaces;

/// <summary>
/// Abstraction for authentication operations (register, login, logout and password change).
/// Defined in the application layer so that CQRS handlers invoke authentication logic
/// without depending on the Infrastructure layer directly.
/// </summary>
public interface IAuthService
{
    Task<Result<object>> RegisterAsync(string username, string email, string password);
    Task<Result<AuthResponse>> LoginAsync(string username, string password);
    Task<Result> LogoutAsync();
    Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}```
## File: \src\Forum.Application\Common\Interfaces\IUnitOfWork.cs
```cs
namespace Forum.Application.Common.Interfaces;

/// <summary>
/// Coordinates the persistence of changes made across multiple repositories in a single database transaction.
/// Lets CQRS handlers call SaveChangesAsync without depending on the Infrastructure layer directly.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}```
## File: \src\Forum.Application\Common\Models\PagedResult.cs
```cs
using System.Text.Json.Serialization;

namespace Forum.Application.Common.Models;

/// <summary>
/// A wrapper for paginated query results.
/// Used by repository methods that return filtered, sorted and paged data.
/// </summary>
/// <typeparam name="T">The type of item in the paged result (like a DTO)</typeparam>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    [JsonConstructor]
    public PagedResult(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
        => new(items, totalCount, pageNumber, pageSize);
}```
## File: \src\Forum.Application\Common\Models\PaginationParams.cs
```cs
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
}```
## File: \src\Forum.Application\Common\Models\Result.cs
```cs
namespace Forum.Application.Common.Models;

/// <summary>
/// Classifies the type of error returned in a Result, mapped to HTTP status codes in the API layer.
/// </summary>
public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Unauthorized,
    Forbidden,
    Conflict
}

/// <summary>
/// Represents the outcome of an operation, carrying an optional error message and ErrorType.
/// Used by all CQRS handlers to communicate success/failure without throwing exceptions.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? error, ErrorType errorType = ErrorType.None)
    {
        if (isSuccess && error != null)
            throw new InvalidOperationException("Success result cannot have an error.");
        if (!isSuccess && error == null)
            throw new InvalidOperationException("Failed result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
        ErrorType = isSuccess ? ErrorType.None : errorType;
    }

    public bool IsSuccess { get; }
    public bool IsFailed => !IsSuccess;
    public string? Error { get; }
    public ErrorType ErrorType { get; }

    public static Result Success() => new (true, null);
    public static Result Failure(string error, ErrorType errorType = ErrorType.Validation) => new (false, error, errorType);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error, ErrorType errorType = ErrorType.Validation) => Result<T>.Failure(error, errorType);
}

/// <summary>
/// Generic variant of Result that also carries a typed Value on success.
/// </summary>
public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, null)
    {
        _value = value;
    }

    private Result(string error, ErrorType errorType = ErrorType.Validation) : base(false, error, errorType)
    {
        _value = default;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");

    public new static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(string error, ErrorType errorType = ErrorType.Validation) => new (error, errorType);
}```
## File: \src\Forum.Application\DTOs\Auth\AuthResponse.cs
```cs
namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO returned after successful login.
/// Includes JWT token to authenticate API calls.
/// </summary>
public record AuthResponse(string Token, string UserId, string Username, string? Email);```
## File: \src\Forum.Application\DTOs\Auth\LoginRequest.cs
```cs
namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO representing a login request.
/// Sent by the client to authenticate and receive a JWT token.
/// </summary>
public record LoginRequest(string Username, string Password);```
## File: \src\Forum.Application\DTOs\Auth\RegisterRequest.cs
```cs
namespace Forum.Application.DTOs.Auth;

/// <summary>
/// DTO representing a registration request.
/// Sent by the client when creating a new account.
/// </summary>
public record RegisterRequest(string Username, string Email, string Password);```
## File: \src\Forum.Application\DTOs\Category\CategoryDto.cs
```cs
namespace Forum.Application.DTOs.Category;

/// <summary>
/// Read-only DTO for returning category information to the client.
/// Includes the thread count for display in category listings.
/// </summary>
public record CategoryDto(
    int CategoryId,
    string Name,
    int ThreadCount
);```
## File: \src\Forum.Application\DTOs\Category\CreateCategoryDto.cs
```cs
namespace Forum.Application.DTOs.Category;

/// <summary>
/// DTO for creating a new category. Contains only the Name field since
/// CategoryId is auto-generated by the database. Only admins can create categories.
/// </summary>
public record CreateCategoryDto(string Name);```
## File: \src\Forum.Application\DTOs\Category\UpdateCategoryDto.cs
```cs
namespace Forum.Application.DTOs.Category;

/// <summary>
/// DTO for updating an existing category.
/// Only admins can update categories and only the Name field can be modified.
/// </summary>
public record UpdateCategoryDto(string Name);```
## File: \src\Forum.Application\DTOs\Comment\CommentDto.cs
```cs
namespace Forum.Application.DTOs.Comment;

/// <summary>
/// Read-only DTO for returning comment data to the client.
/// Handles soft-delete display logic: if IsDeleted is true, Content shows "[deleted]"
/// and AuthorUserName shows "Deleted" (applied in MappingExtensions).
/// Includes parent comment info to support the flat reply display model
/// with an inline citation of the parent comment.
/// </summary>
public record CommentDto(
    int CommentId,
    string Content,
    string AuthorId,
    string AuthorUserName,
    int ThreadId,
    string? ThreadTitle,
    int? ParentCommentId,
    string? ParentCommentAuthorUserName,
    string? ParentCommentContent,
    DateTime TimeCreated,
    int ReplyCount,
    bool IsDeleted,
    int VoteScore,
    int? CurrentUserVote
);```
## File: \src\Forum.Application\DTOs\Comment\CommentFilterParams.cs
```cs
using Forum.Application.Common.Models;

namespace Forum.Application.DTOs.Comment;

/// <summary>
/// Filter parameters for paginated comment queries. Extends PaginationParams
/// to inherit PageNumber and PageSize, then adds comment-specific filters:
/// thread, author, date range, and sort order.
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
}```
## File: \src\Forum.Application\DTOs\Comment\CreateCommentDto.cs
```cs
namespace Forum.Application.DTOs.Comment;

/// <summary>
/// DTO for creating a new comment in a thread.
/// The ThreadId is provided as a route parameter, and the UserId is extracted from the JWT token.
/// </summary>
/// <param name="Content">The text content of the comment.</param>
/// <param name="ParentCommentId">Optional: the ID of the comment being replied to. Null for top-level comments.</param>
public record CreateCommentDto(
    string Content,
    int? ParentCommentId = null
);```
## File: \src\Forum.Application\DTOs\Comment\UpdateCommentDto.cs
```cs
namespace Forum.Application.DTOs.Comment;

/// <summary>
/// DTO for updating an existing comment's content.
/// Only the Content field can be modified. 
/// Authorization checks (owner or admin) are performed in the command handler.
/// </summary>
public record UpdateCommentDto(string Content);```
## File: \src\Forum.Application\DTOs\Thread\CreateThreadDto.cs
```cs
namespace Forum.Application.DTOs.Thread;

/// <summary>
/// DTO for creating a new thread. The Body field will be stored as the first
/// comment in the thread, not as a field on Thread itself.
/// The UserId of the creator is extracted from the JWT token at the API layer.
/// </summary>
public record CreateThreadDto(
    string Title,
    int CategoryId,
    string Body
);```
## File: \src\Forum.Application\DTOs\Thread\ThreadDetailDto.cs
```cs
using Forum.Application.DTOs.Comment;

namespace Forum.Application.DTOs.Thread;

/// <summary>
/// Full-detail DTO used when viewing a single thread. Includes the thread metadata,
/// the body comment, and all subsequent comments.
/// The BodyComment is separated from Comments to allow distinct rendering.
/// </summary>
public record ThreadDetailDto(
    int ThreadId,
    string Title,
    string AuthorId,
    string AuthorUserName,
    int CategoryId,
    string CategoryName,
    DateTime TimeCreated,
    DateTime TimeUpdated,
    CommentDto? BodyComment,
    IReadOnlyList<CommentDto> Comments
);```
## File: \src\Forum.Application\DTOs\Thread\ThreadFilterParams.cs
```cs
using Forum.Application.Common.Models;

namespace Forum.Application.DTOs.Thread;

/// <summary>
/// Filter and sort parameters for paginated thread queries. Extends PaginationParams
/// to inherit PageNumber and PageSize, then adds thread-specific filters:
/// category, author, search text, date range, and sort order.
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
    RecentlyUpdated,
    Category,
    Author
}
```
## File: \src\Forum.Application\DTOs\Thread\ThreadSummaryDto.cs
```cs
namespace Forum.Application.DTOs.Thread;

/// <summary>
/// DTO used in thread listings (e.g. category page).
/// Contains summary information without the full thread body or comments.
/// </summary>
public record ThreadSummaryDto(
    int ThreadId,
    string Title,
    string AuthorId,
    string AuthorUserName,
    int CategoryId,
    string CategoryName,
    DateTime TimeCreated,
    DateTime TimeUpdated,
    int CommentCount,
    string? LastPosterUserName,
    DateTime? LastCommentTime
);```
## File: \src\Forum.Application\DTOs\Thread\UpdateThreadDto.cs
```cs
namespace Forum.Application.DTOs.Thread;

/// <summary>
/// DTO for updating an existing thread's metadata.
/// Both fields are nullable, to support partial updates â€” only provided fields are applied.
/// Updating the thread body is done by editing the first comment, not through this DTO.
/// </summary>
public record UpdateThreadDto(
    string? Title = null,
    int? CategoryId = null
);```
## File: \src\Forum.Application\DTOs\User\ChangePasswordDto.cs
```cs
namespace Forum.Application.DTOs.User;

/// <summary>
/// Payload for changing a user's password.
/// </summary>
public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword
);
```
## File: \src\Forum.Application\DTOs\User\UpdateUserProfileDto.cs
```cs
namespace Forum.Application.DTOs.User;

/// <summary>
/// DTO for updating a user's profile.
/// Both fields are nullable, to support partial updates â€” only provided fields are applied.
/// The user ID is extracted from the JWT token at the API layer.
/// </summary>
public record UpdateUserProfileDto(
    string? UserName = null,
    string? Email = null
);```
## File: \src\Forum.Application\DTOs\User\UserDto.cs
```cs
namespace Forum.Application.DTOs.User;

/// <summary>
/// Read-only DTO for returning basic user information.
/// Used in user listings and references. Handles soft-delete display: if IsDeleted, UserName shows
/// "Deleted User" and Email is null (applied in MappingExtensions).
/// </summary>
public record UserDto(
    string Id,
    string UserName,
    string? Email,
    int ThreadCount,
    int CommentCount,
    bool IsDeleted
);```
## File: \src\Forum.Application\DTOs\User\UserFilterParams.cs
```cs
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
```
## File: \src\Forum.Application\DTOs\User\UserProfileDto.cs
```cs
using Forum.Application.DTOs.Comment;
using Forum.Application.DTOs.Thread;

namespace Forum.Application.DTOs.User;

/// <summary>
/// Extended user DTO for profile pages. Includes all basic user info plus
/// the user's recent threads and comments for displaying activity history.
/// </summary>
public record UserProfileDto(
    string Id,
    string UserName,
    string? Email,
    int ThreadCount,
    int CommentCount,
    bool IsDeleted,
    IReadOnlyList<ThreadSummaryDto> RecentThreads,
    IReadOnlyList<CommentDto> RecentComments
);```
## File: \src\Forum.Application\DTOs\Vote\CastVoteDto.cs
```cs
namespace Forum.Application.DTOs.Vote;

/// <summary>
/// Payload for casting or toggling a vote on a comment.
/// </summary>
/// <param name="Value">Vote direction: 1 (upvote) or -1 (downvote).</param>
public record CastVoteDto(int Value);```
## File: \src\Forum.Application\DTOs\Vote\VoteResponseDto.cs
```cs
namespace Forum.Application.DTOs.Vote;

/// <summary>
/// Returned after a vote is cast, containing the updated score and the user's current vote state.
/// </summary>
public record VoteResponseDto(int CommentId, int NewScore, int UserVote);```
## File: \src\Forum.Application\Features\Auth\Commands\LoginUser.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Auth;
using MediatR;

namespace Forum.Application.Features.Auth.Commands;

/// <summary>
/// Command to authenticate a user and obtain a JWT token.
/// Delegates credential validation and token generation via IAuthService.
/// </summary>
public record LoginUserCommand(string Username, string Password) : IRequest<Result<AuthResponse>>;

/// <summary>
/// Handles the LoginUserCommand by delegating to IAuthService
/// for credential verification and JWT token generation. The auth service validates the
/// username/password pair and returns an AuthResponse containing the token on success.
/// </summary>
public class LoginUserHandler : IRequestHandler<LoginUserCommand, Result<AuthResponse>>
{
    private readonly IAuthService _authService;

    public LoginUserHandler(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Handles the login command by delegating to the auth service.
    /// </summary>
    public async Task<Result<AuthResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        // Delegate entirely to the auth service, which handles credential verification and JWT generation
        return await _authService.LoginAsync(request.Username, request.Password);
    }
}

```
## File: \src\Forum.Application\Features\Auth\Commands\RegisterUser.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using MediatR;

namespace Forum.Application.Features.Auth.Commands;

/// <summary>
/// Command to register a new user account.
/// Delegates the actual registration logic (password hashing, Identity user creation)
/// to IAuthService.
/// </summary>
public record RegisterUserCommand(string Username, string Email, string Password) : IRequest<Result<object>>;

/// <summary>
/// Handles the RegisterUserCommand by delegating to IAuthService
/// for user creation. The auth service is responsible for ASP.NET Core Identity integration,
/// password validation, and any initial role assignment.
/// </summary>
public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, Result<object>>
{
    private readonly IAuthService _authService;

    public RegisterUserHandler(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Handles the registration command by delegating to the auth service.
    /// Forwards the username, email, and password to IAuthService.RegisterAsync
    /// Returns the result (success with user data, or failure with validation errors).
    /// </summary>
    public async Task<Result<object>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Delegate entirely to the auth service, which manages Identity user creation and validation
        return await _authService.RegisterAsync(request.Username, request.Email, request.Password);
    }
}

```
## File: \src\Forum.Application\Features\Categories\Commands\CreateCategory.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;

namespace Forum.Application.Features.Categories.Commands;

/// <summary>
/// Creates a new forum category. Only accessible by admins
/// (authorization enforced at the API endpoint level).
/// </summary>
public record CreateCategoryCommand(string Name) : IRequest<Result<CategoryDto>>;

/// <summary>
/// Handler for CreateCategoryCommand.
/// Validates input, checks for duplicate names, creates the category entity, and persists it to the database.
/// </summary>
public class CreateCategoryHandler : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CategoryDto>("Category name is required.");
        }
        
        if (await _categoryRepository.NameExistsAsync(request.Name, cancellationToken: cancellationToken))
        {
            return Result.Failure<CategoryDto>("A category with this name already exists.", ErrorType.Conflict);
        }
        
        var category = new Category
        {
            Name = request.Name.Trim()
        };
        
        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category.ToCategoryDto());
    }
}

```
## File: \src\Forum.Application\Features\Categories\Commands\DeleteCategory.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Commands;

/// <summary>
/// Deletes a category (hard delete). Only accessible by admins.
/// Prevents deletion if the category still contains threads to avoid orphaned data.
/// </summary>
public record DeleteCategoryCommand(int CategoryId) : IRequest<Result>;

/// <summary>
/// Handler for DeleteCategoryCommand.
/// Validates the category exists and has no threads before performing the hard delete.
/// </summary>
public class DeleteCategoryHandler : IRequestHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdWithThreadsAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result.Failure("Category not found.", ErrorType.NotFound);
        }
        
        if (category.Threads?.Count > 0)
        {
            return Result.Failure("Cannot delete category that contains threads.", ErrorType.Conflict);
        }
        
        _categoryRepository.Delete(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

```
## File: \src\Forum.Application\Features\Categories\Commands\UpdateCategory.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Commands;

/// <summary>
/// Updates an existing category's name. Only accessible by admins
/// (authorization enforced at the API endpoint level).
/// /// </summary>
public record UpdateCategoryCommand(int CategoryId, string Name) : IRequest<Result<CategoryDto>>;

/// <summary>
/// Handler for UpdateCategoryCommand.
/// Validates input, checks for duplicate names, updates and persists changes.
/// </summary>
public class UpdateCategoryHandler : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result.Failure<CategoryDto>("Category not found.", ErrorType.NotFound);
        }
        
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CategoryDto>("Category name is required.");
        }
        
        if (await _categoryRepository.NameExistsAsync(request.Name, request.CategoryId, cancellationToken))
        {
            return Result.Failure<CategoryDto>("A category with this name already exists.", ErrorType.Conflict);
        }
        
        category.Name = request.Name.Trim();
        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(category.ToCategoryDto());
    }
}

```
## File: \src\Forum.Application\Features\Categories\Queries\GetAllCategories.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Queries;

/// <summary>
/// Retrieves all categories with their thread counts.
/// Used by the home page / category listing endpoint.
/// </summary>
public record GetAllCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;

/// <summary>
/// Handler for GetAllCategoriesQuery.
/// Fetches all categories from the repository (with thread counts eagerly loaded) and maps them to DTOs.
/// </summary>
public class GetAllCategoriesHandler : IRequestHandler<GetAllCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetAllCategoriesHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Handles the query by fetching all categories with thread counts from the database,
    /// mapping each entity to a CategoryDto, and wrapping the result in a success Result.
    /// </summary>
    public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetAllCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _categoryRepository.GetAllWithThreadCountAsync(cancellationToken);

        // Map each Category entity to a CategoryDto using the mapping extensions
        var dtos = categories.Select(c => c.ToCategoryDto()).ToList();

        return Result.Success<IReadOnlyList<CategoryDto>>(dtos);
    }
}```
## File: \src\Forum.Application\Features\Categories\Queries\GetCategoryById.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Category;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Categories.Queries;

/// <summary>
/// Retrieves a single category by its ID.
/// Returns a failure Result if the category does not exist.
/// </summary>
public record GetCategoryByIdQuery(int CategoryId) : IRequest<Result<CategoryDto>>;

/// <summary>
/// Handler for GetCategoryByIdQuery.
/// Fetches the category with its threads loaded (for thread count) and maps it to a DTO.
/// </summary>
public class GetCategoryByIdHandler : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryByIdHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Handles the query by looking up the category by ID.
    /// Returns a failure Result if not found, otherwise maps the entity to a CategoryDto.
    /// </summary>
    public async Task<Result<CategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdWithThreadsAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result.Failure<CategoryDto>("Category not found.", ErrorType.NotFound);
        }

        return Result.Success(category.ToCategoryDto());
    }
}```
## File: \src\Forum.Application\Features\Comments\Commands\CreateComment.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;

namespace Forum.Application.Features.Comments.Commands;

/// <summary>
/// Creates a new comment on a thread, optionally as a reply to another comment.
/// Validates that the thread exists, content is not empty, and (if replying) the parent comment
/// exists and belongs to the same thread. Also updates the thread's TimeUpdated timestamp.
/// </summary>
public record CreateCommentCommand(string UserId, int ThreadId, string Content, int? ParentCommentId = null) : IRequest<Result<CommentDto>>;

/// <summary>
/// Handler for CreateCommentCommand.
/// </summary>
public class CreateCommentHandler : IRequestHandler<CreateCommentCommand, Result<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCommentHandler(
        ICommentRepository commentRepository,
        IThreadRepository threadRepository,
        IUnitOfWork unitOfWork)
    {
        _commentRepository = commentRepository;
        _threadRepository = threadRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CommentDto>> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result.Failure<CommentDto>("Comment content is required.");
        }
        
        var thread = await _threadRepository.GetByIdAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure<CommentDto>("Thread not found.", ErrorType.NotFound);
        }
        
        if (request.ParentCommentId.HasValue)
        {
            var parentComment = await _commentRepository.GetByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment == null)
            {
                return Result.Failure<CommentDto>("Parent comment not found.", ErrorType.NotFound);
            }
            
            if (parentComment.ThreadId != request.ThreadId)
            {
                return Result.Failure<CommentDto>("Parent comment must belong to the same thread.");
            }
        }
        
        var comment = new Comment
        {
            Content = request.Content,
            ThreadId = request.ThreadId,
            UserId = request.UserId,
            ParentCommentId = request.ParentCommentId,
            TimeCreated = DateTime.UtcNow
        };
        
        await _commentRepository.AddAsync(comment, cancellationToken);
        
        thread.TimeUpdated = DateTime.UtcNow;
        _threadRepository.Update(thread);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        var createdComment = await _commentRepository.GetByIdWithDetailsAsync(comment.CommentId, cancellationToken);
        return Result.Success(createdComment!.ToCommentDto());
    }
}```
## File: \src\Forum.Application\Features\Comments\Commands\DeleteComment.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Commands;

/// <summary>
/// Soft-deletes a comment (sets IsDeleted = true).
/// Only the comment owner or an admin can perform this action.
/// The first comment in a thread (which serves as the thread body) cannot be deleted
/// â€” the entire thread must be deleted in that case.
/// Soft-deleted comments display as "[deleted]" with replies preserved.
/// </summary>
public record DeleteCommentCommand(int CommentId, string UserId, bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handler for DeleteCommentCommand.
/// Validates the comment exists, is not already deleted, the user is authorized,
/// and the comment is not the thread body (first comment), before performing the soft-delete.
/// </summary>
public class DeleteCommentHandler : IRequestHandler<DeleteCommentCommand, Result>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCommentHandler(ICommentRepository commentRepository, IUnitOfWork unitOfWork)
    {
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _commentRepository.GetByIdWithDetailsAsync(request.CommentId, cancellationToken);
        if (comment == null)
        {
            return Result.Failure("Comment not found.", ErrorType.NotFound);
        }
        
        if (comment.IsDeleted)
        {
            return Result.Failure("Comment is already deleted.", ErrorType.Conflict);
        }
        
        if (comment.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure("You are not authorized to delete this comment.", ErrorType.Forbidden);
        }

        // The first comment in a thread serves as the thread body and cannot be deleted independently. 
        var firstComment = await _commentRepository.GetFirstCommentByThreadIdAsync(comment.ThreadId, cancellationToken);
        if (firstComment != null && firstComment.CommentId == request.CommentId)
        {
            return Result.Failure("Cannot delete the thread body comment. Delete the thread instead.", ErrorType.Conflict);
        }

        // Soft-delete: set IsDeleted flag (content will display as "[deleted]", replies are preserved)
        comment.IsDeleted = true;
        _commentRepository.Update(comment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}```
## File: \src\Forum.Application\Features\Comments\Commands\UpdateComment.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Commands;

/// <summary>
/// Updates the content of an existing comment.
/// Only the comment owner or an admin can perform this action. Soft-deleted comments cannot be updated.
/// </summary>
public record UpdateCommentCommand(int CommentId, string Content, string UserId, bool IsAdmin) : IRequest<Result<CommentDto>>;

/// <summary>
/// Handler for UpdateCommentCommand. Validates the comment exists, is not
/// soft-deleted, the user is authorized, and the new content is valid before applying the update.
/// </summary>
public class UpdateCommentHandler : IRequestHandler<UpdateCommentCommand, Result<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCommentHandler(ICommentRepository commentRepository, IUnitOfWork unitOfWork)
    {
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<CommentDto>> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        var comment = await _commentRepository.GetByIdWithDetailsAsync(request.CommentId, cancellationToken);
        if (comment == null)
        {
            return Result.Failure<CommentDto>("Comment not found.", ErrorType.NotFound);
        }
        
        if (comment.IsDeleted)
        {
            return Result.Failure<CommentDto>("Cannot update a deleted comment.", ErrorType.Conflict);
        }
        
        if (comment.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure<CommentDto>("You are not authorized to update this comment.", ErrorType.Forbidden);
        }
        
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result.Failure<CommentDto>("Comment content is required.");
        }
        
        comment.Content = request.Content;
        _commentRepository.Update(comment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(comment.ToCommentDto());
    }
}```
## File: \src\Forum.Application\Features\Comments\Queries\GetCommentById.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves a single comment by its ID, including related details
/// (user info, parent comment reference). Returns a failure if the comment does not exist.
/// </summary>
public record GetCommentByIdQuery(int CommentId) : IRequest<Result<CommentDto>>;

/// <summary>
/// Handler for GetCommentByIdQuery.
/// Fetches a comment with its related data (User, ParentComment) and maps it to a DTO.
/// </summary>
public class GetCommentByIdHandler : IRequestHandler<GetCommentByIdQuery, Result<CommentDto>>
{
    private readonly ICommentRepository _commentRepository;

    public GetCommentByIdHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query by fetching the comment with its details (user, parent comment)
    /// from the repository and mapping it to a CommentDto.
    /// </summary>
    public async Task<Result<CommentDto>> Handle(GetCommentByIdQuery request, CancellationToken cancellationToken)
    {
        var comment = await _commentRepository.GetByIdWithDetailsAsync(request.CommentId, cancellationToken);
        if (comment == null)
        {
            return Result.Failure<CommentDto>("Comment not found.", ErrorType.NotFound);
        }

        // Map to DTO (soft-delete display logic applied in mapping: "[deleted]" content, "Deleted User" username)
        return Result.Success(comment.ToCommentDto());
    }
}```
## File: \src\Forum.Application\Features\Comments\Queries\GetComments.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves a paginated, filterable list of comments across all threads.
/// Uses CommentFilterParams to support pagination, sorting, and filtering.
/// </summary>
public record GetCommentsQuery(CommentFilterParams FilterParams) : IRequest<Result<PagedResult<CommentDto>>>;

/// <summary>
/// Handler for GetCommentsQuery.
/// Fetches a paged set of comments from the repository based on the provided filter parameters and maps them to DTOs.
/// </summary>
public class GetCommentsHandler : IRequestHandler<GetCommentsQuery, Result<PagedResult<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;

    public GetCommentsHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query by fetching a paged subset of comments from the database
    /// using the provided filter/pagination parameters, mapping each entity to a
    /// CommentDto, and returning the result wrapped in a PagedResult{T}.
    /// </summary>
    public async Task<Result<PagedResult<CommentDto>>> Handle(GetCommentsQuery request, CancellationToken cancellationToken)
    {
        // Fetch the paged comments and total count from the repository using filter params
        var (comments, totalCount) = await _commentRepository.GetPagedAsync(request.FilterParams, cancellationToken);

        // Map each Comment entity to a CommentDto (includes soft-delete display logic)
        var dtos = comments.Select(c => c.ToCommentDto()).ToList();

        // Wrap the results in a PagedResult with pagination metadata
        return Result.Success(PagedResult<CommentDto>.Create(
            dtos,
            totalCount,
            request.FilterParams.PageNumber,
            request.FilterParams.PageSize
        ));
    }
}

```
## File: \src\Forum.Application\Features\Comments\Queries\GetCommentsByThread.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves a paginated list of comments belonging to a specific thread.
/// Used by the thread detail page to display the discussion.
/// </summary>
public record GetCommentsByThreadQuery(
    int ThreadId,
    int PageNumber = 1,
    int PageSize = 50,
    string? CurrentUserId = null) : IRequest<Result<PagedResult<CommentDto>>>;

/// <summary>
/// Handler for GetCommentsByThreadQuery.
/// Verifies the thread exists, then fetches and paginates the comments for that thread.
/// </summary>
public class GetCommentsByThreadHandler : IRequestHandler<GetCommentsByThreadQuery, Result<PagedResult<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly IVoteRepository _voteRepository;

    public GetCommentsByThreadHandler(
        ICommentRepository commentRepository,
        IThreadRepository threadRepository,
        IVoteRepository voteRepository)
    {
        _commentRepository = commentRepository;
        _threadRepository = threadRepository;
        _voteRepository = voteRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<PagedResult<CommentDto>>> Handle(GetCommentsByThreadQuery request, CancellationToken cancellationToken)
    {
        if (!await _threadRepository.ExistsAsync(t => t.ThreadId == request.ThreadId, cancellationToken))
            return Result.Failure<PagedResult<CommentDto>>("Thread not found.", ErrorType.NotFound);

        var paginationParams = new PaginationParams
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        var (comments, totalCount) = await _commentRepository.GetPagedByThreadIdAsync(
            request.ThreadId, paginationParams, cancellationToken);

        // Batch-load vote data (2 queries total, not per-comment)
        var commentIds = comments.Select(c => c.CommentId).ToList();
        var scores = await _voteRepository.GetScoresForCommentsAsync(commentIds, cancellationToken);

        Dictionary<int, int> userVotes = request.CurrentUserId != null
            ? await _voteRepository.GetUserVotesForCommentsAsync(request.CurrentUserId, commentIds, cancellationToken)
            : new();

        var dtos = comments.Select(c => c.ToCommentDto(
            scores.GetValueOrDefault(c.CommentId, 0),
            request.CurrentUserId != null ? userVotes.GetValueOrDefault(c.CommentId, 0) : null
        )).ToList();

        return Result.Success(PagedResult<CommentDto>.Create(
            dtos, totalCount, paginationParams.PageNumber, paginationParams.PageSize));
    }
}```
## File: \src\Forum.Application\Features\Comments\Queries\GetCommentsByUser.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves the most recent comments made by a specific user.
/// Used for user profile pages to display recent activity.
/// </summary>
public record GetCommentsByUserQuery(string UserId, int Count = 10) : IRequest<Result<IReadOnlyList<CommentDto>>>;

/// <summary>
/// Handler for GetCommentsByUserQuery.
/// Fetches a limited number of recent comments for the specified user and maps them to DTOs.
/// </summary>
public class GetCommentsByUserHandler : IRequestHandler<GetCommentsByUserQuery, Result<IReadOnlyList<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;

    public GetCommentsByUserHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query by fetching up to Count recent comments for the user
    /// from the repository and mapping each to a CommentDto.
    /// </summary>
    public async Task<Result<IReadOnlyList<CommentDto>>> Handle(GetCommentsByUserQuery request, CancellationToken cancellationToken)
    {
        // Fetch the user's most recent comments (limited by Count), ordered by creation date
        var comments = await _commentRepository.GetByUserIdAsync(request.UserId, request.Count, cancellationToken);

        // Map each Comment entity to a CommentDto (includes soft-delete display logic)
        var dtos = comments.Select(c => c.ToCommentDto()).ToList();

        return Result.Success<IReadOnlyList<CommentDto>>(dtos);
    }
}

```
## File: \src\Forum.Application\Features\Comments\Queries\GetReplies.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Comments.Queries;

/// <summary>
/// Retrieves all direct replies to a specific comment.
/// Validates that the parent comment exists before fetching replies.
/// Replies use the self-referencing ParentCommentId relationship for flat display
/// with a "replying to @username" reference.
/// </summary>
public record GetRepliesQuery(int CommentId) : IRequest<Result<IReadOnlyList<CommentDto>>>;

/// <summary>
/// Handler for GetRepliesQuery.
/// Verifies the parent comment exists, then fetches all direct replies and maps them to DTOs.
/// </summary>
public class GetRepliesHandler : IRequestHandler<GetRepliesQuery, Result<IReadOnlyList<CommentDto>>>
{
    private readonly ICommentRepository _commentRepository;

    public GetRepliesHandler(ICommentRepository commentRepository)
    {
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<IReadOnlyList<CommentDto>>> Handle(GetRepliesQuery request, CancellationToken cancellationToken)
    {
        if (!await _commentRepository.ExistsAsync(c => c.CommentId == request.CommentId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<CommentDto>>("Comment not found.");
        }
        
        var replies = await _commentRepository.GetRepliesAsync(request.CommentId, cancellationToken);
        
        var dtos = replies.Select(c => c.ToCommentDto()).ToList();

        return Result.Success<IReadOnlyList<CommentDto>>(dtos);
    }
}

```
## File: \src\Forum.Application\Features\Threads\Commands\CreateThread.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Application.Features.Threads.Commands;

/// <summary>
/// Creates a new thread in a category.
/// The thread body is stored as the first comment (not as a field on Thread).
/// Any authenticated user can create threads.
/// </summary>
public record CreateThreadCommand(string UserId, string Title, int CategoryId, string Body) : IRequest<Result<ThreadDetailDto>>;

/// <summary>
/// Handler for CreateThreadCommand.
/// Validates input, creates both the thread entity and its body comment, persists them, then returns the full thread detail.
/// </summary>
public class CreateThreadHandler : IRequestHandler<CreateThreadCommand, Result<ThreadDetailDto>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public CreateThreadHandler(
        IThreadRepository threadRepository,
        ICategoryRepository categoryRepository,
        ICommentRepository commentRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _threadRepository = threadRepository;
        _categoryRepository = categoryRepository;
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<ThreadDetailDto>> Handle(CreateThreadCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure<ThreadDetailDto>("Thread title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return Result.Failure<ThreadDetailDto>("Thread body is required.");
        }
        
        if (!await _categoryRepository.ExistsAsync(c => c.CategoryId == request.CategoryId, cancellationToken))
        {
            return Result.Failure<ThreadDetailDto>("Category not found.", ErrorType.NotFound);
        }

        var now = DateTime.UtcNow;
        
        var thread = new ThreadEntity
        {
            Title = request.Title.Trim(),
            CategoryId = request.CategoryId,
            UserId = request.UserId,
            TimeCreated = now,
            TimeUpdated = now
        };
        
        await _threadRepository.AddAsync(thread, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Create the body comment (thread body = first comment)
        var bodyComment = new Comment
        {
            Content = request.Body,
            ThreadId = thread.ThreadId,
            UserId = request.UserId,
            TimeCreated = now
        };

        // Second save: persist the body comment
        await _commentRepository.AddAsync(bodyComment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-query the thread to get the fully populated detail DTO
        // (reuses the GetThreadById query handler)
        return await _mediator.Send(new Queries.GetThreadByIdQuery(thread.ThreadId), cancellationToken);
    }
}```
## File: \src\Forum.Application\Features\Threads\Commands\DeleteThread.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Commands;

/// <summary>
/// Deletes a thread (hard delete).
/// All associated comments are removed via cascade delete.
/// Authorization: only the thread owner or an admin can delete threads.
/// </summary>
public record DeleteThreadCommand(int ThreadId, string UserId, bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handler for DeleteThreadCommand.
/// Validates the thread exists, checks authorization, and performs a hard delete with cascade.
/// </summary>
public class DeleteThreadHandler : IRequestHandler<DeleteThreadCommand, Result>
{
    private readonly IThreadRepository _threadRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteThreadHandler(IThreadRepository threadRepository, IUnitOfWork unitOfWork)
    {
        _threadRepository = threadRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result> Handle(DeleteThreadCommand request, CancellationToken cancellationToken)
    {
        var thread = await _threadRepository.GetByIdWithDetailsAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure("Thread not found.", ErrorType.NotFound);
        }
        
        if (thread.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure("You are not authorized to delete this thread.", ErrorType.Forbidden);
        }

        // Hard delete â€” EF Core cascade will remove all associated comments
        _threadRepository.Delete(thread);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}```
## File: \src\Forum.Application\Features\Threads\Commands\UpdateThread.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Commands;

/// <summary>
/// Updates an existing thread's metadata (title and/or category).
/// Both Title and CategoryId are optional â€” only provided fields are applied.
/// Authorization: only the thread owner or an admin can update.
/// </summary>
public record UpdateThreadCommand(int ThreadId, string? Title, int? CategoryId, string UserId, bool IsAdmin) : IRequest<Result<ThreadDetailDto>>;

/// <summary>
/// Handler for UpdateThreadCommand.
/// Validates ownership/admin access, applies partial updates, and persists changes.
/// </summary>
public class UpdateThreadHandler : IRequestHandler<UpdateThreadCommand, Result<ThreadDetailDto>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public UpdateThreadHandler(
        IThreadRepository threadRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _threadRepository = threadRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _mediator = mediator;
    }

    /// <summary>
    /// Handles the command.
    /// </summary>
    public async Task<Result<ThreadDetailDto>> Handle(UpdateThreadCommand request, CancellationToken cancellationToken)
    {
        var thread = await _threadRepository.GetByIdWithDetailsAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure<ThreadDetailDto>("Thread not found.", ErrorType.NotFound);
        }
        
        if (thread.UserId != request.UserId && !request.IsAdmin)
        {
            return Result.Failure<ThreadDetailDto>("You are not authorized to update this thread.", ErrorType.Forbidden);
        }
        
        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Result.Failure<ThreadDetailDto>("Thread title cannot be empty.");
            }
            thread.Title = request.Title.Trim();
        }
        
        if (request.CategoryId.HasValue)
        {
            if (!await _categoryRepository.ExistsAsync(c => c.CategoryId == request.CategoryId.Value, cancellationToken))
            {
                return Result.Failure<ThreadDetailDto>("Category not found.", ErrorType.NotFound);
            }
            thread.CategoryId = request.CategoryId.Value;
        }
        
        thread.TimeUpdated = DateTime.UtcNow;
        _threadRepository.Update(thread);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return await _mediator.Send(new Features.Threads.Queries.GetThreadByIdQuery(request.ThreadId), cancellationToken);
    }
}```
## File: \src\Forum.Application\Features\Threads\Queries\GetThreadById.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves a single thread with full details including the body comment
/// and all reply comments. Used for the thread detail page.
/// </summary>
public record GetThreadByIdQuery(int ThreadId) : IRequest<Result<ThreadDetailDto>>;

/// <summary>
/// Handler for GetThreadByIdQuery.
/// Fetches the thread with comments, identifies the body comment (first comment),
/// separates it from replies, and returns a DTO.
/// </summary>
public class GetThreadByIdHandler : IRequestHandler<GetThreadByIdQuery, Result<ThreadDetailDto>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICommentRepository _commentRepository;

    public GetThreadByIdHandler(IThreadRepository threadRepository, ICommentRepository commentRepository)
    {
        _threadRepository = threadRepository;
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<ThreadDetailDto>> Handle(GetThreadByIdQuery request, CancellationToken cancellationToken)
    {
        var thread = await _threadRepository.GetByIdWithCommentsAsync(request.ThreadId, cancellationToken);
        if (thread == null)
        {
            return Result.Failure<ThreadDetailDto>("Thread not found.", ErrorType.NotFound);
        }
        
        var bodyComment = await _commentRepository.GetFirstCommentByThreadIdAsync(request.ThreadId, cancellationToken);

        // Filter out the body comment from the reply list, sort chronologically, and map to DTOs.
        var comments = thread.Comments?
            .Where(c => c.CommentId != bodyComment?.CommentId) 
            .OrderBy(c => c.TimeCreated)                      
            .Select(c => c.ToCommentDto())
            .ToList() ?? new List<DTOs.Comment.CommentDto>();

        // Map to detail DTO with body and comments separated.
        return Result.Success(thread.ToThreadDetailDto(bodyComment, comments));
    }
}```
## File: \src\Forum.Application\Features\Threads\Queries\GetThreads.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves a paginated, filtered, and sorted list of threads.
/// Supports filtering by category, author, search term, date range, and sort order.
/// Used by the main thread listing page and search functionality.
/// </summary>
public record GetThreadsQuery(ThreadFilterParams FilterParams) : IRequest<Result<PagedResult<ThreadSummaryDto>>>;

/// <summary>
/// Handler for GetThreadsQuery.
/// Delegates filtering/pagination to the repository and maps the results to summary DTOs wrapped in a PagedResult.
/// </summary>
public class GetThreadsHandler : IRequestHandler<GetThreadsQuery, Result<PagedResult<ThreadSummaryDto>>>
{
    private readonly IThreadRepository _threadRepository;

    public GetThreadsHandler(IThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<PagedResult<ThreadSummaryDto>>> Handle(GetThreadsQuery request, CancellationToken cancellationToken)
    {
        // Fetch paged threads; repository handles filtering, sorting, and pagination
        var (threads, totalCount) = await _threadRepository.GetPagedAsync(request.FilterParams, cancellationToken);

        // Map Thread entities to summary DTOs
        var dtos = threads.Select(t => t.ToThreadSummaryDto()).ToList();

        // Wrap in PagedResult with pagination metadata
        return Result.Success(PagedResult<ThreadSummaryDto>.Create(
            dtos,
            totalCount,
            request.FilterParams.PageNumber,
            request.FilterParams.PageSize
        ));
    }
}

```
## File: \src\Forum.Application\Features\Threads\Queries\GetThreadsByCategory.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves all threads in a specific category.
/// First validates that the category exists, then fetches its threads.
/// Used for the category detail page.
/// </summary>
public record GetThreadsByCategoryQuery(int CategoryId) : IRequest<Result<IReadOnlyList<ThreadSummaryDto>>>;

/// <summary>
/// Handler for GetThreadsByCategoryQuery.
/// Validates the category exists and fetches all its threads as summary DTOs.
/// </summary>
public class GetThreadsByCategoryHandler : IRequestHandler<GetThreadsByCategoryQuery, Result<IReadOnlyList<ThreadSummaryDto>>>
{
    private readonly IThreadRepository _threadRepository;
    private readonly ICategoryRepository _categoryRepository;

    public GetThreadsByCategoryHandler(IThreadRepository threadRepository, ICategoryRepository categoryRepository)
    {
        _threadRepository = threadRepository;
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// Handles the query.
    /// </summary>
    public async Task<Result<IReadOnlyList<ThreadSummaryDto>>> Handle(GetThreadsByCategoryQuery request, CancellationToken cancellationToken)
    {
        if (!await _categoryRepository.ExistsAsync(c => c.CategoryId == request.CategoryId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ThreadSummaryDto>>("Category not found.", ErrorType.NotFound);
        }
        
        var threads = await _threadRepository.GetByCategoryIdAsync(request.CategoryId, cancellationToken);
        var dtos = threads.Select(t => t.ToThreadSummaryDto()).ToList();
        return Result.Success<IReadOnlyList<ThreadSummaryDto>>(dtos);
    }
}

```
## File: \src\Forum.Application\Features\Threads\Queries\GetThreadsByUser.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Thread;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Threads.Queries;

/// <summary>
/// Retrieves a summary of a user's most recent threads.
/// Used for user profile pages to show recent thread activity.
/// </summary>
public record GetThreadsByUserQuery(string UserId, int Count = 10) : IRequest<Result<IReadOnlyList<ThreadSummaryDto>>>;

/// <summary>
/// Handler for GetThreadsByUserQuery.
/// Fetches the user's recent threads from the repository and maps them to summary DTOs.
/// </summary>
public class GetThreadsByUserHandler : IRequestHandler<GetThreadsByUserQuery, Result<IReadOnlyList<ThreadSummaryDto>>>
{
    private readonly IThreadRepository _threadRepository;

    public GetThreadsByUserHandler(IThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }

    /// <summary>
    /// Handles the query by fetching the specified number of recent threads
    /// for the given user and mapping them to ThreadSummaryDto objects.
    /// </summary>
    public async Task<Result<IReadOnlyList<ThreadSummaryDto>>> Handle(GetThreadsByUserQuery request, CancellationToken cancellationToken)
    {
        var threads = await _threadRepository.GetByUserIdAsync(request.UserId, request.Count, cancellationToken);
        var dtos = threads.Select(t => t.ToThreadSummaryDto()).ToList();
        return Result.Success<IReadOnlyList<ThreadSummaryDto>>(dtos);
    }
}```
## File: \src\Forum.Application\Features\Users\Commands\ChangePassword.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using MediatR;

namespace Forum.Application.Features.Users.Commands;

/// <summary>
/// Changes a user's password after verifying the current password.
/// Owners can change their own; admins can change any user's password.
/// </summary>
public record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword,
    string RequestingUserId,
    bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handles <see cref="ChangePasswordCommand"/> â€” authorisation check then delegates to <see cref="IAuthService"/>.
/// </summary>
public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IAuthService _authService;

    public ChangePasswordHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId != request.RequestingUserId && !request.IsAdmin)
            return Result.Failure("You are not authorized to change this password.", ErrorType.Forbidden);

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return Result.Failure("New password cannot be empty.");

        return await _authService.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword);
    }
}
```
## File: \src\Forum.Application\Features\Users\Commands\DeleteUser.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Commands;

/// <summary>
/// Command to soft-delete a user account. The user's IsDeleted flag is set to true,
/// causing their display name to appear as "Deleted User" throughout the forum.
/// Only the user themselves or an admin can perform this action.
/// </summary>
public record DeleteUserCommand(string UserId, string RequestingUserId, bool IsAdmin) : IRequest<Result>;

/// <summary>
/// Handles the DeleteUserCommand by performing a soft-delete on the user.
/// The user entity is not removed from the database; instead, the IsDeleted flag is set to true,
/// preserving referential integrity for existing threads, comments, and replies.
/// </summary>
public class DeleteUserHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command to soft-delete a user.
    /// </summary>
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId != request.RequestingUserId && !request.IsAdmin)
        {
            return Result.Failure("You are not authorized to delete this user.", ErrorType.Forbidden);
        }
        
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure("User not found.", ErrorType.NotFound);
        }
        
        if (user.IsDeleted)
        {
            return Result.Failure("User is already deleted.", ErrorType.Conflict);
        }
        
        user.IsDeleted = true;
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}

```
## File: \src\Forum.Application\Features\Users\Commands\UpdateUserProfile.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Commands;

/// <summary>
/// Command to update a user's profile information (username and/or email).
/// Supports authorization: only the user themselves or an admin can perform this update.
/// </summary>
public record UpdateUserProfileCommand(
    string UserId,
    string? UserName,
    string? Email,
    string RequestingUserId,
    bool IsAdmin) : IRequest<Result<UserDto>>;

/// <summary>
/// Handles the UpdateUserProfileCommand by validating authorization,
/// checking uniqueness constraints, updating the user entity, and persisting changes.
/// </summary>
public class UpdateUserProfileHandler : IRequestHandler<UpdateUserProfileCommand, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserProfileHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the command to update a user's profile.
    /// </summary>
    public async Task<Result<UserDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId != request.RequestingUserId && !request.IsAdmin)
        {
            return Result.Failure<UserDto>("You are not authorized to update this profile.", ErrorType.Forbidden);
        }
        
        var user = await _userRepository.GetByIdWithDetailsAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Failure<UserDto>("User not found.", ErrorType.NotFound);
        }
        
        if (user.IsDeleted)
        {
            return Result.Failure<UserDto>("Cannot update a deleted user.", ErrorType.Conflict);
        }
        
        if (request.UserName != null)
        {
            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                return Result.Failure<UserDto>("Username cannot be empty.");
            }
            
            if (await _userRepository.UserNameExistsAsync(request.UserName, request.UserId, cancellationToken))
            {
                return Result.Failure<UserDto>("Username is already taken.", ErrorType.Conflict);
            }
            
            user.UserName = request.UserName.Trim();
            user.NormalizedUserName = request.UserName.Trim().ToUpperInvariant();
        }
        
        if (request.Email != null)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Result.Failure<UserDto>("Email cannot be empty.");
            }
            
            if (await _userRepository.EmailExistsAsync(request.Email, request.UserId, cancellationToken))
            {
                return Result.Failure<UserDto>("Email is already in use.", ErrorType.Conflict);
            }
            
            user.Email = request.Email.Trim();
            user.NormalizedEmail = request.Email.Trim().ToUpperInvariant();
        }
        
        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success(user.ToUserDto());
    }
}

```
## File: \src\Forum.Application\Features\Users\Queries\GetAllUsers.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Queries;

/// <summary>
/// Retrieves all users with their thread and comment counts.
/// Used by the admin panel to list and manage user accounts.
/// </summary>
public record GetAllUsers : IRequest<Result<List<UserDto>>>;

/// <summary>
/// Handler for GetAllUsers.
/// Fetches all users from the repository (with threads and comments eagerly loaded) and maps them to DTOs.
/// Soft-deleted users are included so admins can see their status.
/// </summary>
public class GetAllUsersHandler : IRequestHandler<GetAllUsers, Result<List<UserDto>>>
{
    private readonly IUserRepository _userRepository;

    public GetAllUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Handles the query by fetching all users from the database,
    /// mapping each entity to a UserDto, and wrapping the result in a success Result.
    /// </summary>
    public async Task<Result<List<UserDto>>> Handle(GetAllUsers request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        
        var dtos = users.Select(u => u.ToUserDto()).ToList();

        return Result.Success(dtos);
    }
}
```
## File: \src\Forum.Application\Features\Users\Queries\GetPagedUsers.cs
```cs
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
public record GetPagedUsersQuery(int PageNumber = 1, int PageSize = 10, UserSortBy SortBy = UserSortBy.Username) : IRequest<Result<PagedResult<UserDto>>>;

/// <summary>
/// Handles GetPagedUsersQuery by fetching a filtered page of users from the repository.
/// </summary>
public class GetPagedUsersHandler : IRequestHandler<GetPagedUsersQuery, Result<PagedResult<UserDto>>>
{
    private readonly IUserRepository _userRepository;

    public GetPagedUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<PagedResult<UserDto>>> Handle(GetPagedUsersQuery request, CancellationToken cancellationToken)
    {
        var filterParams = new UserFilterParams
        {
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            SortBy = request.SortBy
        };

        var (users, totalCount) = await _userRepository.GetPagedAsync(filterParams, cancellationToken);

        var dtos = users.Select(u => u.ToUserDto()).ToList();

        return Result.Success(PagedResult<UserDto>.Create(
            dtos,
            totalCount,
            filterParams.PageNumber,
            filterParams.PageSize
        ));
    }
}
```
## File: \src\Forum.Application\Features\Users\Queries\GetUserById.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Queries;

/// <summary>
/// Query to retrieve a single user by their unique identifier.
/// </summary>
public record GetUserByIdQuery(string UserId) : IRequest<Result<UserDto>>;

/// <summary>
/// Handles the GetUserByIdQuery by looking up the user in
/// the repository and mapping the result to a UserDto.
/// </summary>
public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// Handles the query to retrieve a user by their ID.
    /// </summary>
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithDetailsAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure<UserDto>("User not found.", ErrorType.NotFound);
        }
        
        return Result.Success(user.ToUserDto());
    }
}

```
## File: \src\Forum.Application\Features\Users\Queries\GetUserProfile.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Application.Mappings;
using Forum.Application.Repositories;
using MediatR;

namespace Forum.Application.Features.Users.Queries;

/// <summary>
/// Query to retrieve a user's full profile, including their recent threads and comments.
/// </summary>
public record GetUserProfileQuery(string UserId) : IRequest<Result<UserProfileDto>>;

/// <summary>
/// Handles the GetUserProfileQuery by aggregating user information,
/// recent threads, and recent comments into a UserProfileDto.
/// </summary>
public class GetUserProfileHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly ICommentRepository _commentRepository;

    public GetUserProfileHandler(
        IUserRepository userRepository,
        IThreadRepository threadRepository,
        ICommentRepository commentRepository)
    {
        _userRepository = userRepository;
        _threadRepository = threadRepository;
        _commentRepository = commentRepository;
    }

    /// <summary>
    /// Handles the query to build a full user profile with recent activity.
    /// </summary>
    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithDetailsAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            return Result.Failure<UserProfileDto>("User not found.", ErrorType.NotFound);
        }
        
        var recentThreads = await _threadRepository.GetByUserIdAsync(request.UserId, 5, cancellationToken);
        
        var recentComments = await _commentRepository.GetByUserIdAsync(request.UserId, 5, cancellationToken);
        
        var threadDtos = recentThreads.Select(t => t.ToThreadSummaryDto()).ToList();
        var commentDtos = recentComments.Select(c => c.ToCommentDto()).ToList();
        
        return Result.Success(user.ToUserProfileDto(threadDtos, commentDtos));
    }
}

```
## File: \src\Forum.Application\Features\Votes\Commands\CastVote.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Vote;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using MediatR;

namespace Forum.Application.Features.Votes.Commands;

/// <summary>
/// Casts, toggles, or switches a user's vote on a comment.
/// If the user has no vote, creates one. If same direction, removes it. If opposite, switches.
/// </summary>
/// <summary>
/// Command to cast a vote on a comment.
/// </summary>
/// <param name="UserId">The unique identifier of the user casting the vote</param>
/// <param name="CommentId">The ID of the comment being voted on</param>
/// <param name="Value">Vote direction: 1 for upvote, -1 for downvote</param>
public record CastVoteCommand(string UserId, int CommentId, int Value) : IRequest<Result<VoteResponseDto>>;

/// <summary>
/// Handles CastVoteCommand — manages vote creation, toggle, and direction switch.
/// </summary>
/// <summary>
/// Handles the CastVoteCommand by implementing the vote casting business logic.
/// </summary>
public class CastVoteHandler : IRequestHandler<CastVoteCommand, Result<VoteResponseDto>>
{
    private readonly IVoteRepository _voteRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the CastVoteHandler class.
    /// </summary>
    /// <param name="voteRepository">Repository for vote operations</param>
    /// <param name="commentRepository">Repository for comment operations</param>
    /// <param name="unitOfWork">Unit of work for transaction management</param>
    public CastVoteHandler(IVoteRepository voteRepository, ICommentRepository commentRepository, IUnitOfWork unitOfWork)
    {
        _voteRepository = voteRepository;
        _commentRepository = commentRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Handles the CastVoteCommand by processing the vote request.
    /// </summary>
    /// <param name="request">The vote command containing user ID, comment ID, and vote value</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing VoteResponseDto with updated vote information</returns>
    public async Task<Result<VoteResponseDto>> Handle(CastVoteCommand request, CancellationToken cancellationToken)
    {
        // Validate vote value - only allow 1 (upvote) or -1 (downvote)
        if (request.Value != 1 && request.Value != -1)
            return Result.Failure<VoteResponseDto>("Vote value must be 1 or -1.", ErrorType.Validation);

        // Verify that the comment exists before allowing voting
        if (!await _commentRepository.ExistsAsync(c => c.CommentId == request.CommentId, cancellationToken))
            return Result.Failure<VoteResponseDto>("Comment not found.", ErrorType.NotFound);

        // Check if user has already voted on this comment
        var existing = await _voteRepository.GetByUserAndCommentAsync(request.UserId, request.CommentId, cancellationToken);

        if (existing == null)
        {
            // New vote
            await _voteRepository.AddAsync(new Vote
            {
                UserId = request.UserId,
                CommentId = request.CommentId,
                Value = request.Value
            }, cancellationToken);
        }
        else if (existing.Value == request.Value)
        {
            // Same direction — toggle off
            _voteRepository.Delete(existing);
        }
        else
        {
            // Opposite direction — switch
            existing.Value = request.Value;
            _voteRepository.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var scores = await _voteRepository.GetScoresForCommentsAsync(
            new[] { request.CommentId }, cancellationToken);
        var newScore = scores.GetValueOrDefault(request.CommentId, 0);

        // Determine a user's current vote after the operation
        var userVotes = await _voteRepository.GetUserVotesForCommentsAsync(
            request.UserId, new[] { request.CommentId }, cancellationToken);
        var userVote = userVotes.GetValueOrDefault(request.CommentId, 0);

        return Result.Success(new VoteResponseDto(request.CommentId, newScore, userVote));
    }
}
```
## File: \src\Forum.Application\Mappings\MappingExtensions.cs
```cs
using Forum.Application.DTOs.Category;
using Forum.Application.DTOs.Comment;
using Forum.Application.DTOs.Thread;
using Forum.Application.DTOs.User;
using Forum.Domain.Entities;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Application.Mappings;

/// <summary>
/// Centralized extension methods for mapping domain entities to DTOs.
/// Entity-to-DTO conversion logic lives here, including soft-delete display rules.
/// </summary>
public static class MappingExtensions
{
    private const string DeletedUserName = "Deleted User";
    private const string DeletedCommentContent = "[deleted]";
    private const string DeletedAuthorName = "Deleted";

    /// <summary>
    /// Maps a Category entity to a CategoryDto, including the count of threads.
    /// </summary>
    public static CategoryDto ToCategoryDto(this Category category)
    {
        return new CategoryDto(
            category.CategoryId,
            category.Name,
            category.Threads?.Count ?? 0
        );
    }

    /// <summary>
    /// Maps a Thread entity to a ThreadSummaryDto for use in thread listings.
    /// Checks if the author has been soft-deleted and replaces their name accordingly.
    /// </summary>
    public static ThreadSummaryDto ToThreadSummaryDto(this ThreadEntity thread)
    {
        var authorUserName = thread.User?.IsDeleted == true
            ? DeletedUserName
            : thread.User?.UserName ?? string.Empty;

        var lastComment = thread.Comments?
            .OrderByDescending(c => c.TimeCreated)
            .FirstOrDefault();

        var lastPosterUserName = lastComment?.User?.IsDeleted == true
            ? DeletedUserName
            : lastComment?.User?.UserName;

        return new ThreadSummaryDto(
            thread.ThreadId,
            thread.Title,
            thread.UserId,
            authorUserName,
            thread.CategoryId,
            thread.Category?.Name ?? string.Empty,
            thread.TimeCreated,
            thread.TimeUpdated,
            thread.Comments?.Count ?? 0,
            lastPosterUserName,
            lastComment?.TimeCreated
        );
    }

    /// <summary>
    /// Maps a Thread entity to a ThreadDetailDto for the thread detail view.
    /// Separates the body comment (first comment) from the rest of the comments.
    /// </summary>
    public static ThreadDetailDto ToThreadDetailDto(this ThreadEntity thread, Comment? bodyComment, IReadOnlyList<CommentDto> comments)
    {
        var authorUserName = thread.User?.IsDeleted == true
            ? DeletedUserName
            : thread.User?.UserName ?? string.Empty;

        return new ThreadDetailDto(
            thread.ThreadId,
            thread.Title,
            thread.UserId,
            authorUserName,
            thread.CategoryId,
            thread.Category?.Name ?? string.Empty,
            thread.TimeCreated,
            thread.TimeUpdated,
            bodyComment?.ToCommentDto(),
            comments
        );
    }

    /// <summary>
    /// Maps a Comment entity to a CommentDto. Implements the soft-delete display logic.
    /// voteScore and currentUserVote are supplied externally by the query handler
    /// (batch-loaded, not per-comment queries).
    /// </summary>
    public static CommentDto ToCommentDto(this Comment comment, int voteScore = 0, int? currentUserVote = null)
    {
        var isDeleted = comment.IsDeleted;
        var authorIsDeleted = comment.User?.IsDeleted == true;

        var content = isDeleted ? DeletedCommentContent : comment.Content;

        // Determine the author display name based on deletion states:
        var authorUserName = isDeleted
            ? DeletedAuthorName
            : authorIsDeleted
                ? DeletedUserName
                : comment.User?.UserName ?? string.Empty;

        // Resolve parent comment author name for reply citation display
        var parentAuthorUserName = comment.ParentComment?.User?.IsDeleted == true
            ? DeletedUserName
            : comment.ParentComment?.User?.UserName;

        // Resolve parent comment content for the inline citation box
        var parentCommentContent = comment.ParentComment?.IsDeleted == true
            ? DeletedCommentContent
            : comment.ParentComment?.Content;

        return new CommentDto(
            comment.CommentId,
            content,
            comment.UserId,
            authorUserName,
            comment.ThreadId,
            comment.Thread?.Title,
            comment.ParentCommentId,
            parentAuthorUserName,
            parentCommentContent,
            comment.TimeCreated,
            comment.Replies?.Count ?? 0,
            isDeleted,
            voteScore,
            currentUserVote
        );
    }

    /// <summary>
    /// Maps a User entity to a UserDto. Applies soft-delete display rules.
    /// </summary>
    public static UserDto ToUserDto(this User user)
    {
        var userName = user.IsDeleted ? DeletedUserName : user.UserName ?? string.Empty;
        return new UserDto(
            user.Id,
            userName,
            user.IsDeleted ? null : user.Email,
            user.Threads?.Count ?? 0,
            user.Comments?.Count ?? 0,
            user.IsDeleted
        );
    }

    /// <summary>
    /// Maps a User entity to a UserProfileDto, which includes recent activity.
    /// </summary>
    public static UserProfileDto ToUserProfileDto(
        this User user,
        IReadOnlyList<ThreadSummaryDto> recentThreads,
        IReadOnlyList<CommentDto> recentComments)
    {
        var userName = user.IsDeleted ? DeletedUserName : user.UserName ?? string.Empty;
        return new UserProfileDto(
            user.Id,
            userName,
            user.IsDeleted ? null : user.Email,
            user.Threads?.Count ?? 0,
            user.Comments?.Count ?? 0,
            user.IsDeleted,
            recentThreads,
            recentComments
        );
    }
}```
## File: \src\Forum.Application\Repositories\ICategoryRepository.cs
```cs
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom category-specific methods.
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetByIdWithThreadsAsync(int categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllWithThreadCountAsync(CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default);
}```
## File: \src\Forum.Application\Repositories\ICommentRepository.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom comment-specific methods.
/// </summary>
public interface ICommentRepository : IRepository<Comment>
{
    Task<Comment?> GetByIdWithDetailsAsync(int commentId, CancellationToken cancellationToken = default);
    
    Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedByThreadIdAsync(
        int threadId,
        PaginationParams paginationParams,
        CancellationToken cancellationToken = default);
    
    Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedAsync(
        CommentFilterParams filterParams,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Comment>> GetByUserIdAsync(string userId, int count,
        CancellationToken cancellationToken = default);

    // Returns the first comment in a thread (used as the thread body).
    Task<Comment?> GetFirstCommentByThreadIdAsync(int threadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Comment>> GetRepliesAsync(int commentId, CancellationToken cancellationToken = default);
}```
## File: \src\Forum.Application\Repositories\IRepository.cs
```cs
using System.Linq.Expressions;

namespace Forum.Application.Repositories;

/// <summary>
/// Generic repository interface defining standard CRUD operations across all entities.
/// Serves as the base contract that all entity-specific repositories extend.
/// </summary>
/// <typeparam name="T"> Entity type </typeparam>
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(object id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
}```
## File: \src\Forum.Application\Repositories\IThreadRepository.cs
```cs
using Forum.Application.DTOs.Thread;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom thread-specific methods.
/// Using alias (ThreadEntity) to avoid conflict with System.Threading.Thread.
/// </summary>
public interface IThreadRepository : IRepository<ThreadEntity>
{
    Task<ThreadEntity?> GetByIdWithDetailsAsync(int threadId, CancellationToken cancellationToken = default);
    Task<ThreadEntity?> GetByIdWithCommentsAsync(int threadId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ThreadEntity> Items, int TotalCount)> GetPagedAsync(
        ThreadFilterParams filterParams,
        CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<ThreadEntity>> GetByUserIdAsync(string userId, int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThreadEntity>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default);
}```
## File: \src\Forum.Application\Repositories\IUserRepository.cs
```cs
using Forum.Application.DTOs.User;
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Extends the generic repository interface with custom user-specific methods.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdWithDetailsAsync(string userId, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<bool> UserNameExistsAsync(string userName, string? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, string? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(UserFilterParams filterParams, CancellationToken cancellationToken = default);
}```
## File: \src\Forum.Application\Repositories\IVoteRepository.cs
```cs
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Repository for managing comment votes (upvotes/downvotes).
/// </summary>
public interface IVoteRepository : IRepository<Vote>
{
    // Retrieves the vote a specific user has cast on a specific comment, or null if none exists
    Task<Vote?> GetByUserAndCommentAsync(string userId, int commentId, CancellationToken ct = default);

    // Returns a dictionary mapping CommentId → net score (sum of all vote values) for the given comments
    Task<Dictionary<int, int>> GetScoresForCommentsAsync(IEnumerable<int> commentIds, CancellationToken ct = default);

    // Returns a dictionary mapping CommentId → the current user's vote value (1, -1, or absent) for the given comments
    Task<Dictionary<int, int>> GetUserVotesForCommentsAsync(string userId, IEnumerable<int> commentIds, CancellationToken ct = default);
}```
## File: \src\Forum.Application\DependencyInjection.cs
```cs
using Microsoft.Extensions.DependencyInjection;

namespace Forum.Application;

/// <summary>
/// Extension methods for registering Application layer services with the DI container.
/// Called from the API's Program.cs during application startup to wire up all
/// Application-layer dependencies.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Application layer services into the dependency injection container.
    /// Registers MediatR, which scans the assembly to auto-discover all
    /// IRequest/IRequestHandler pairs (CQRS command and query handlers in the Features folder).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}```
## File: \src\Forum.Blazor\Components\Layout\MainLayout.razor
```razor
@inherits LayoutComponentBase

<div class="site-wrapper">
    <NavMenu /> <!-- Top navigation bar, rendered on all pages -->

    <main class="main-body">
        @Body <!-- Renders the content of the currently matched page component -->
    </main>

    @if (!IsHomePage)
    {
        <!-- Footer is hidden on the home page to preserve its full-screen hero layout -->
        <footer class="main-footer">
            <div class="header-container footer-content">
                <div class="footer-links">
                    | <a href="#" onclick="window.scrollTo({ top: 0, behavior: 'smooth' }); return false;">Back to the top</a> |
                </div>
                <p>&copy; 2026 Overtime Digital Media. All rights reserved.</p>
            </div>
        </footer>
    }
</div>

@code {
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; } // Provides access to the current HTTP context (e.g. auth state)

    // Returns true if the current URL matches the app root, used to hide the footer on the home page
    private bool IsHomePage => NavigationManager.Uri.TrimEnd('/') == NavigationManager.BaseUri.TrimEnd('/');

    [Inject] private NavigationManager NavigationManager { get; set; } = default!; // Used to read the current URL
}```
## File: \src\Forum.Blazor\Components\Layout\NavMenu.razor
```razor
@using Forum.Blazor.Services
@inject CategoryService CategoryService      
@inject IAuthClientService AuthService        
@inject NavigationManager Navigation          

<header class="main-header">
    <div class="top-row-black">
        <div class="header-container">
            <div class="brand">
                <a href="/">OVER<span class="red-text">TIME</span></a> @* Site logo/wordmark linking to home *@
            </div>
            <span class="brand-tagline">Debates on current affairs and recent past of your favourite sports.</span>
            <div class="user-actions desktop-only">
                <AuthorizeView>
                    <Authorized>
                        @* Show username as a link to the profile page when logged in *@
                        <a href="/profile" class="user-greeting">
                            @context.User.Identity?.Name
                        </a>
                        @* Only render the Admin button if the user has the Admin role *@
                        <AuthorizeView Roles="Admin">
                            <Authorized Context="adminContext">
                                <a href="/admin" class="btn-outline">Admin</a>
                            </Authorized>
                        </AuthorizeView>
                        <button class="btn-outline" @onclick="HandleLogout">Log Out</button>
                    </Authorized>
                    <NotAuthorized>
                        @* Show login/register buttons to unauthenticated users *@
                        <a href="/login" class="btn-outline">Log In</a>
                        <a href="/register" class="btn-red">Register</a>
                    </NotAuthorized>
                </AuthorizeView>
            </div>
            @* Hamburger button â€” toggles the mobile nav collapse via Bootstrap *@
            <button class="mobile-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navMenuCollapse" aria-controls="navMenuCollapse" aria-expanded="false" aria-label="Toggle navigation">
                <span></span>
                <span></span>
                <span></span>
            </button>
        </div>
    </div>

    @* Desktop category navigation bar â€” dynamically populated from the API *@
    <nav class="sub-nav-white desktop-only">
        <div class="header-container">
            <ul class="nav-menu">
                @if (_categories != null)
                {
                    @foreach (var category in _categories)
                    {
                        <li><a href="/category/@category.Name">@category.Name</a></li>
                    }
                }
            </ul>
        </div>
    </nav>

    @* Mobile collapsible menu â€” contains categories and auth actions *@
    <div class="collapse" id="navMenuCollapse">
        <div class="mobile-nav-menu">
            @if (_categories != null)
            {
                @foreach (var category in _categories)
                {
                    <a href="/category/@category.Name" class="mobile-nav-link">@category.Name</a>
                }
            }
            <div class="mobile-nav-auth">
                <AuthorizeView>
                    <Authorized>
                        <a href="/profile" class="mobile-nav-link">@context.User.Identity?.Name</a>
                        <AuthorizeView Roles="Admin">
                            <Authorized Context="adminCtx">
                                <a href="/admin" class="mobile-nav-link">Admin</a>
                            </Authorized>
                        </AuthorizeView>
                        <button class="mobile-nav-link mobile-nav-btn" @onclick="HandleLogout">Log Out</button>
                    </Authorized>
                    <NotAuthorized>
                        <a href="/login" class="mobile-nav-link">Log In</a>
                        <a href="/register" class="mobile-nav-link">Register</a>
                    </NotAuthorized>
                </AuthorizeView>
            </div>
        </div>
    </div>
</header>

@code {
    private List<CategoryDto>? _categories; // Holds the list of categories fetched on init

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Fetch all categories to populate the nav menus
            _categories = await CategoryService.GetAllAsync();
        }
        catch
        {
            // Fall back to an empty list if the API is unavailable
            _categories = [];
        }
    }

    private async Task HandleLogout()
    {
        // Clear the auth session then force a full page reload to reset all state
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("/", forceLoad: true);
    }
}```
## File: \src\Forum.Blazor\Components\Layout\NavMenu.razor.css
```css
/* Hamburger toggle button — styled with an inline SVG background icon */
.navbar-toggler {
    appearance: none;
    cursor: pointer;
    width: 3.5rem;
    height: 2.5rem;
    color: white;
    position: absolute;
    top: 0.5rem;
    right: 1rem;
    border: 1px solid rgba(255, 255, 255, 0.1);
    background: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 30 30'%3e%3cpath stroke='rgba%28255, 255, 255, 0.55%29' stroke-linecap='round' stroke-miterlimit='10' stroke-width='2' d='M4 7h22M4 15h22M4 23h22'/%3e%3c/svg%3e") no-repeat center/1.75rem rgba(255, 255, 255, 0.1);
}
/* Highlight the toggler when checked (nav is open) */
.navbar-toggler:checked {
    background-color: rgba(255, 255, 255, 0.5);
}
/* Semi-transparent top bar behind the nav content */
.top-row {
    height: 3.5rem;
    background-color: rgba(0,0,0,0.4);
}
.navbar-brand {
    font-size: 1.1rem;
}
/* Base styles for Bootstrap Icon sprites used as nav item icons */
.bi {
    display: inline-block;
    position: relative;
    width: 1.25rem;
    height: 1.25rem;
    margin-right: 0.75rem;
    top: -1px;
    background-size: cover;
}
/* Home icon — inline SVG data URI */
.bi-house-door-fill-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-house-door-fill' viewBox='0 0 16 16'%3E%3Cpath d='M6.5 14.5v-3.505c0-.245.25-.495.5-.495h2c.25 0 .5.25.5.5v3.5a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5v-7a.5.5 0 0 0-.146-.354L13 5.793V2.5a.5.5 0 0 0-.5-.5h-1a.5.5 0 0 0-.5.5v1.293L8.354 1.146a.5.5 0 0 0-.708 0l-6 6A.5.5 0 0 0 1.5 7.5v7a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5Z'/%3E%3C/svg%3E");
}
/* Plus/create icon — inline SVG data URI */
.bi-plus-square-fill-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-plus-square-fill' viewBox='0 0 16 16'%3E%3Cpath d='M2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2zm6.5 4.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3a.5.5 0 0 1 1 0z'/%3E%3C/svg%3E");
}
/* Nested list icon — inline SVG data URI */
.bi-list-nested-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-list-nested' viewBox='0 0 16 16'%3E%3Cpath fill-rule='evenodd' d='M4.5 11.5A.5.5 0 0 1 5 11h10a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 3 7h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 1 3h10a.5.5 0 0 1 0 1H1a.5.5 0 0 1-.5-.5z'/%3E%3C/svg%3E");
}
/* Individual nav items with vertical spacing */
.nav-item {
    font-size: 0.9rem;
    padding-bottom: 0.5rem;
}
/* Extra top padding on the first item */
.nav-item:first-of-type {
    padding-top: 1rem;
}
/* Extra bottom padding on the last item */
.nav-item:last-of-type {
    padding-bottom: 1rem;
}
/* Nav link inside Blazor scoped child components — ::deep pierces the scope boundary */
.nav-item ::deep .nav-link {
    color: #d7d7d7;
    background: none;
    border: none;
    border-radius: 4px;
    height: 3rem;
    display: flex;
    align-items: center;
    line-height: 3rem;
    width: 100%;
}
/* Highlight the active route link */
.nav-item ::deep a.active {
    background-color: rgba(255,255,255,0.37);
    color: white;
}
/* Subtle hover highlight on nav links */
.nav-item ::deep .nav-link:hover {
    background-color: rgba(255,255,255,0.1);
    color: white;
}
/* Nav content is hidden by default on mobile — toggled by the checkbox hack */
.nav-scrollable {
    display: none;
}
/* Show nav content when the hamburger toggler is checked */
.navbar-toggler:checked ~ .nav-scrollable {
    display: block;
}
/* On larger screens: always show nav and hide the toggler */
@media (min-width: 641px) {
    .navbar-toggler {
        display: none;
    }
    .nav-scrollable {
        display: block;
        height: calc(100vh - 3.5rem);
        overflow-y: auto;
    }
}```
## File: \src\Forum.Blazor\Components\Pages\Admin.razor
```razor
@page "/admin"
@inject CategoryService CategoryService  
@inject ThreadService ThreadService       
@inject UserService UserService           
@using Forum.Application.DTOs.Thread
@using Forum.Application.DTOs.User
@inject IJSRuntime JS                     

<PageTitle>Admin Panel - Sport Forum</PageTitle>

<link rel="stylesheet" href="/css/panel.css" />

<AuthorizeView Roles="Admin">
<NotAuthorized>
    @* Shown to any user who is not in the Admin role *@
    <div class="row justify-content-center mt-5">
        <div class="col-md-6 text-center">
            <h3>Access Denied</h3>
            <p>You must be an admin to view this page.</p>
            <a href="/login" class="btn btn-primary">Log In</a>
        </div>
    </div>
</NotAuthorized>
<Authorized>

<div class="panel-wrapper">
    <nav class="breadcrumb-trail">
        SPORT FORUM > <strong>ADMIN PANEL</strong>
    </nav>

    <h1 class="panel-title">Admin Panel</h1>

    @* Tab buttons clicking a tab sets activeTab and shows the corresponding section *@
    <div class="panel-tabs">
        <button class="panel-tab @(activeTab == "categories" ? "active" : "")" @onclick='() => activeTab = "categories"'>Categories</button>
        <button class="panel-tab @(activeTab == "threads" ? "active" : "")" @onclick='() => activeTab = "threads"'>Threads</button>
        <button class="panel-tab @(activeTab == "users" ? "active" : "")" @onclick='() => activeTab = "users"'>Users</button>
    </div>

    @* Status banner shown after any create/update/delete operation *@
    @if (!string.IsNullOrEmpty(statusMessage))
    {
        <div class="panel-status @(isError ? "error" : "success")">@statusMessage</div>
    }

    @* ===== CATEGORIES TAB ===== *@
    @if (activeTab == "categories")
    {
        <div class="panel-section panel-section-narrow">
            <h2>Manage Categories</h2>

            @* Inline form for creating a new category *@
            <div class="panel-add-form">
                <input type="text" class="panel-input" placeholder="New category name..." @bind="newCategoryName" />
                <button class="btn-red" @onclick="AddCategory" disabled="@isLoading">Add Category</button>
            </div>

            <div class="panel-table-wrap">
            <table class="panel-table">
                <thead>
                    <tr>
                        <th>ID</th>
                        <th>Name</th>
                        <th>Threads</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var cat in categories)
                    {
                        <tr>
                            <td>@cat.CategoryId</td>
                            <td>@cat.Name</td>
                            <td>@cat.ThreadCount</td>
                            <td>
                                @* Delete is disabled if the category contains threads to prevent orphaned data *@
                                <button class="btn-danger-sm"
                                        @onclick="() => DeleteCategory(cat.CategoryId, cat.Name)"
                                        disabled="@(cat.ThreadCount > 0)"
                                        title="@(cat.ThreadCount > 0 ? "Cannot delete: category has threads" : "Delete category")">
                                    Delete
                                </button>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
            </div>
        </div>
    }

    @* ===== THREADS TAB ===== *@
    @if (activeTab == "threads")
    {
        <div class="panel-section">
            <h2>Manage Threads</h2>

            <div class="d-flex align-items-center mb-2">
                @* Page size selector â€” resets to page 1 on change *@
                <label class="me-2 mb-0 panel-page-size-label">Threads per page:</label>
                <select class="form-select form-select-sm w-auto" value="@threadsPageSize" @onchange="OnThreadsPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                @* Sort selector â€” resets to page 1 on change *@
                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_threadsSortByString" @onchange="OnThreadsSortChanged">
                    <option value="@ThreadSortBy.Newest.ToString()">Newest</option>
                    <option value="@ThreadSortBy.Oldest.ToString()">Oldest</option>
                    <option value="@ThreadSortBy.Category.ToString()">Category</option>
                    <option value="@ThreadSortBy.Author.ToString()">Author</option>
                </select>

                <div class="ms-auto">
                    @* Pagination controls â€” only rendered when there is more than one page *@
                    @if (threadsPaged != null && threadsPaged.TotalPages > 1)
                    {
                        <nav aria-label="Threads pagination">
                            <ul class="pagination pagination-sm mb-0">
                                <li class="page-item @(threadsPage == 1 ? "disabled" : "")">
                                    <button class="page-link" @onclick="PrevThreadsPage">Prev</button>
                                </li>

                                @{
                                    var ttPages = threadsPaged.TotalPages;
                                    var ttShow = new SortedSet<int>();
                                    // Always show first and last page, plus neighbours of the current page
                                    ttShow.Add(1);
                                    ttShow.Add(ttPages);
                                    if (threadsPage > 1) ttShow.Add(threadsPage - 1);
                                    ttShow.Add(threadsPage);
                                    if (threadsPage < ttPages) ttShow.Add(threadsPage + 1);

                                    int? ttLast = null;
                                    foreach (var pageNum in ttShow)
                                    {
                                        // Insert ellipsis between non-consecutive page numbers
                                        if (ttLast.HasValue && pageNum - ttLast.Value > 1)
                                        {
                                            <li class="page-item disabled">
                                                <span class="page-link">...</span>
                                            </li>
                                        }
                                        <li class="page-item @(pageNum == threadsPage ? "active" : "")">
                                            <button class="page-link" @onclick="() => GoToThreadsPage(pageNum)">@pageNum</button>
                                        </li>
                                        ttLast = pageNum;
                                    }
                                }

                                <li class="page-item @(threadsPage == threadsPaged.TotalPages ? "disabled" : "")">
                                    <button class="page-link" @onclick="NextThreadsPage">Next</button>
                                </li>
                            </ul>
                        </nav>
                    }
                </div>
            </div>

            <div class="panel-table-wrap">
            <table class="panel-table">
                <thead>
                    <tr>
                        <th>ID</th>
                        <th>Title</th>
                        <th>Author</th>
                        <th>Category</th>
                        <th>Created</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @if (threads != null)
                    {
                        @foreach (var t in threads)
                        {
                            <tr>
                                <td>@t.ThreadId</td>
                                <td>@t.Title</td>
                                <td>@t.AuthorUserName</td>
                                <td>@t.CategoryName</td>
                                <td>@t.TimeCreated.ToString("yyyy-MM-dd")</td>
                                <td>
                                    <button class="btn-danger-sm" @onclick="() => DeleteThread(t.ThreadId, t.Title)">Delete</button>
                                </td>
                            </tr>
                        }
                    }
                </tbody>
            </table>
            </div>
        </div>
    }

    @* ===== USERS TAB ===== *@
    @if (activeTab == "users")
    {
        <div class="panel-section">
            <h2>Manage Users</h2>

            <div class="d-flex align-items-center mb-2">
                @* Page size selector â€” resets to page 1 on change *@
                <label class="me-2 mb-0 panel-page-size-label">Users per page:</label>
                <select class="form-select form-select-sm w-auto" value="@usersPageSize" @onchange="OnUsersPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                @* Sort selector â€” resets to page 1 on change *@
                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_usersSortByString" @onchange="OnUsersSortChanged">
                    <option value="@UserSortBy.Username.ToString()">Username</option>
                    <option value="@UserSortBy.Threads.ToString()">Threads</option>
                    <option value="@UserSortBy.Comments.ToString()">Comments</option>
                </select>

                <div class="ms-auto">
                    @* Pagination controls â€” only rendered when there is more than one page *@
                    @if (usersPaged != null && usersPaged.TotalPages > 1)
                    {
                        <nav aria-label="Users pagination">
                            <ul class="pagination pagination-sm mb-0">
                                <li class="page-item @(usersPage == 1 ? "disabled" : "")">
                                    <button class="page-link" @onclick="PrevUsersPage">Prev</button>
                                </li>

                                @{
                                    var uuPages = usersPaged.TotalPages;
                                    var uuShow = new SortedSet<int>();
                                    // Always show first and last page, plus neighbours of the current page
                                    uuShow.Add(1);
                                    uuShow.Add(uuPages);
                                    if (usersPage > 1) uuShow.Add(usersPage - 1);
                                    uuShow.Add(usersPage);
                                    if (usersPage < uuPages) uuShow.Add(usersPage + 1);

                                    int? uuLast = null;
                                    foreach (var pageNum in uuShow)
                                    {
                                        // Insert ellipsis between non-consecutive page numbers
                                        if (uuLast.HasValue && pageNum - uuLast.Value > 1)
                                        {
                                            <li class="page-item disabled">
                                                <span class="page-link">...</span>
                                            </li>
                                        }
                                        <li class="page-item @(pageNum == usersPage ? "active" : "")">
                                            <button class="page-link" @onclick="() => GoToUsersPage(pageNum)">@pageNum</button>
                                        </li>
                                        uuLast = pageNum;
                                    }
                                }

                                <li class="page-item @(usersPage == usersPaged.TotalPages ? "disabled" : "")">
                                    <button class="page-link" @onclick="NextUsersPage">Next</button>
                                </li>
                            </ul>
                        </nav>
                    }
                </div>
            </div>

            <div class="panel-table-wrap">
            <table class="panel-table">
                <thead>
                    <tr>
                        <th>Username</th>
                        <th>Email</th>
                        <th>Threads</th>
                        <th>Comments</th>
                        <th>Status</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @if (users != null)
                    {
                        @foreach (var user in users)
                        {
                            <tr>
                                <td>
                                    @* Inline username editing â€” shows an input field when this row is being edited *@
                                    @if (editingUserId == user.Id)
                                    {
                                        <input type="text" class="panel-input-inline" @bind="editingUserName" />
                                    }
                                    else
                                    {
                                        @user.UserName
                                    }
                                </td>
                                <td>@(user.Email ?? "â€”")</td>
                                <td>@user.ThreadCount</td>
                                <td>@user.CommentCount</td>
                                <td>@(user.IsDeleted ? "Deleted" : "Active")</td>
                                <td class="panel-actions">
                                    @* Actions are hidden for already-deleted (soft-deleted) users *@
                                    @if (!user.IsDeleted)
                                    {
                                        @if (editingUserId == user.Id)
                                        {
                                            @* Editing mode â€” show Save/Cancel *@
                                            <button class="btn-success-sm" @onclick="() => SaveUserName(user.Id)">Save</button>
                                            <button class="btn-outline-sm" @onclick="CancelEditUser">Cancel</button>
                                        }
                                        else
                                        {
                                            @* View mode â€” show Rename/Delete *@
                                            <button class="btn-outline-sm" @onclick="() => StartEditUser(user.Id, user.UserName)">Rename</button>
                                            <button class="btn-danger-sm" @onclick="() => DeleteUser(user.Id, user.UserName)">Delete</button>
                                        }
                                    }
                                </td>
                            </tr>
                        }
                    }
                </tbody>
            </table>
            </div>
        </div>
    }

</div>

</Authorized>
</AuthorizeView>

@code {
    private string activeTab = "categories"; // Currently active tab: "categories", "threads", or "users"
    private string statusMessage = "";       // Feedback message shown after admin actions
    private bool isError;                    // True if statusMessage represents an error
    private bool isLoading;                  // Prevents double-submits during async operations

    // Categories
    private List<CategoryDto> categories = [];
    private string newCategoryName = "";

    // Threads
    private List<ThreadSummaryDto> threads = [];
    private PagedResult<ThreadSummaryDto>? threadsPaged;
    private int threadsPage = 1;
    private int threadsPageSize = 5;
    private ThreadSortBy _threadsSortBy = ThreadSortBy.Newest;
    private string _threadsSortByString => _threadsSortBy.ToString(); // Bound to the sort <select> value

    // Users
    private List<UserDto> users = [];
    private PagedResult<UserDto>? usersPaged;
    private int usersPage = 1;
    private int usersPageSize = 5;
    private UserSortBy _usersSortBy = UserSortBy.Username;
    private string _usersSortByString => _usersSortBy.ToString(); // Bound to the sort <select> value
    private string? editingUserId;       // ID of the user row currently being renamed, or null
    private string editingUserName = ""; // Holds the draft username during inline editing

    private bool _loaded; // Guard flag to prevent double-loading on re-render

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Load all data once after the first render, using OnAfterRenderAsync to support
        // InteractiveServer render mode where OnInitializedAsync may run before auth is available
        if (firstRender && !_loaded)
        {
            _loaded = true;
            await LoadCategories();
            await LoadThreads();
            await LoadUsers();
            StateHasChanged();
        }
    }

    private void SetStatus(string message, bool error = false)
    {
        // Updates the status banner with a success or error message
        statusMessage = message;
        isError = error;
    }

    // ===== Categories =====
    private async Task LoadCategories()
    {
        categories = await CategoryService.GetAllAsync();
    }

    private async Task AddCategory()
    {
        if (string.IsNullOrWhiteSpace(newCategoryName)) return;
        isLoading = true;
        var response = await CategoryService.CreateAsync(new CreateCategoryDto(newCategoryName.Trim()));
        if (response.IsSuccessStatusCode)
        {
            newCategoryName = "";  // Clear the input on success
            await LoadCategories();
            SetStatus("Category created.");
        }
        else
        {
            SetStatus("Failed to create category.", true);
        }
        isLoading = false;
    }

    private async Task DeleteCategory(int id, string name)
    {
        // Confirm before deleting â€” warns that all threads in the category will also be removed
        var confirmed = await JS.InvokeAsync<bool>("confirm", $"Delete category \"{name}\"? All threads in it will also be deleted.");
        if (!confirmed) return;

        var response = await CategoryService.DeleteAsync(id);
        if (response.IsSuccessStatusCode)
        {
            await LoadCategories();
            SetStatus($"Category \"{name}\" deleted.");
        }
        else
        {
            SetStatus("Failed to delete category.", true);
        }
    }

    // ===== Threads =====
    private async Task LoadThreads()
    {
        threadsPaged = await ThreadService.GetThreadsAsync(new ThreadFilterParams { PageNumber = threadsPage, PageSize = threadsPageSize, SortBy = _threadsSortBy });
        threads = threadsPaged?.Items?.ToList() ?? [];
    }

    private async Task OnThreadsSortChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!Enum.TryParse<ThreadSortBy>(e.Value.ToString(), out var parsed)) return;
        if (parsed == _threadsSortBy) return; // No-op if same sort selected
        _threadsSortBy = parsed;
        threadsPage = 1; // Reset to first page on sort change
        await LoadThreads();
    }

    private async Task DeleteThread(int id, string title)
    {
        // Confirm before deleting a thread
        var confirmed = await JS.InvokeAsync<bool>("confirm", $"Delete thread \"{title}\"?");
        if (!confirmed) return;

        var response = await ThreadService.DeleteAsync(id);
        if (response.IsSuccessStatusCode)
        {
            await LoadThreads();
            SetStatus($"Thread \"{title}\" deleted.");
        }
        else
        {
            SetStatus("Failed to delete thread.", true);
        }
    }

    private async Task OnThreadsPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == threadsPageSize) return; // No-op if same size selected
        threadsPageSize = newSize;
        threadsPage = 1; // Reset to first page on page size change
        await LoadThreads();
    }

    private async Task PrevThreadsPage()
    {
        if (threadsPage > 1) { threadsPage--; await LoadThreads(); }
    }

    private async Task NextThreadsPage()
    {
        if (threadsPaged != null && threadsPage < threadsPaged.TotalPages) { threadsPage++; await LoadThreads(); }
    }

    private async Task GoToThreadsPage(int p)
    {
        if (threadsPaged == null || p < 1 || p > threadsPaged.TotalPages) return;
        threadsPage = p;
        await LoadThreads();
    }

    // ===== Users =====
    private async Task LoadUsers()
    {
        try
        {
            usersPaged = await UserService.GetPagedAsync(usersPage, usersPageSize, _usersSortBy.ToString());
            users = usersPaged?.Items?.ToList() ?? [];
        }
        catch (HttpRequestException)
        {
            // API returns 403 if the cookie has expired or role claim is missing
            SetStatus("Failed to load users. You may not have admin permissions.", true);
        }
    }

    private async Task OnUsersSortChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!Enum.TryParse<UserSortBy>(e.Value.ToString(), out var parsed)) return;
        if (parsed == _usersSortBy) return; // No-op if same sort selected
        _usersSortBy = parsed;
        usersPage = 1; // Reset to first page on sort change
        await LoadUsers();
    }

    private async Task OnUsersPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == usersPageSize) return; // No-op if same size selected
        usersPageSize = newSize;
        usersPage = 1; // Reset to first page on page size change
        await LoadUsers();
    }

    private async Task PrevUsersPage()
    {
        if (usersPage > 1) { usersPage--; await LoadUsers(); }
    }

    private async Task NextUsersPage()
    {
        if (usersPaged != null && usersPage < usersPaged.TotalPages) { usersPage++; await LoadUsers(); }
    }

    private async Task GoToUsersPage(int p)
    {
        if (usersPaged == null || p < 1 || p > usersPaged.TotalPages) return;
        usersPage = p;
        await LoadUsers();
    }

    private void StartEditUser(string id, string currentName)
    {
        // Enter inline edit mode for the selected user row
        editingUserId = id;
        editingUserName = currentName;
    }

    private void CancelEditUser()
    {
        // Exit inline edit mode without saving
        editingUserId = null;
        editingUserName = "";
    }

    private async Task SaveUserName(string id)
    {
        if (string.IsNullOrWhiteSpace(editingUserName)) return;

        var response = await UserService.UpdateAsync(id, new UpdateUserProfileDto(editingUserName.Trim(), null));
        if (response.IsSuccessStatusCode)
        {
            editingUserId = null;
            editingUserName = "";
            await LoadUsers();
            SetStatus("Username updated.");
        }
        else
        {
            SetStatus("Failed to update username.", true);
        }
    }

    private async Task DeleteUser(string id, string name)
    {
        // Soft-delete â€” the user record is flagged as deleted, not removed from the database
        var confirmed = await JS.InvokeAsync<bool>("confirm", $"Delete user \"{name}\"? This is a soft-delete.");
        if (!confirmed) return;

        var response = await UserService.DeleteAsync(id);
        if (response.IsSuccessStatusCode)
        {
            await LoadUsers();
            SetStatus($"User \"{name}\" deleted.");
        }
        else
        {
            SetStatus("Failed to delete user.", true);
        }
    }

}```
## File: \src\Forum.Blazor\Components\Pages\CategoryThreads.razor
```razor
@page "/category/{CategoryName}"
@using Forum.Application.DTOs.Thread
@inject CategoryService CategoryService   
@inject ThreadService ThreadService       

<PageTitle>@CategoryName - Sport Forum</PageTitle>

<link rel="stylesheet" href="/css/category.css" />
<link rel="stylesheet" href="/css/forum-table.css" />

<div class="category-page">
    @* Hero banner â€” CSS class is derived from the category name to allow per-category background images *@
    <div class="category-hero @(CategoryName?.ToLower().Replace(" ", ""))">
        <div class="hero-content">
            <h1 class="category-title">@CategoryName?.ToUpper()</h1>
        </div>
    </div>

    @* Loading / empty / content states *@
    @if (_threadsPage == null)
    {
        <div class="loading">loading threads...</div>
    }
    else if (!_threadsPage.Items.Any())
    {
        <div class="no-threads">no threads in @CategoryName yet. be the first to start a discussion!</div>
    }
    else
    {
        <div class="forum-view-wrapper">
            <div class="d-flex align-items-center mb-2">
                @* Page size selector â€” resets to page 1 on change *@
                <label class="me-2 mb-0" style="color: #8b949e;">Threads per page:</label>
                <select class="form-select form-select-sm w-auto category-pagination-select" value="@_pageSize" @onchange="OnPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                @* Sort selector â€” resets to page 1 on change *@
                <label class="ms-3 me-2 mb-0" style="color: #8b949e;">Sort:</label>
                <select class="form-select form-select-sm w-auto category-sort-select" value="@_sortByString" @onchange="OnSortChanged">
                    <option value="@ThreadSortBy.Newest.ToString()">Newest</option>
                    <option value="@ThreadSortBy.Oldest.ToString()">Oldest</option>
                    <option value="@ThreadSortBy.MostComments.ToString()">Most comments</option>
                    <option value="@ThreadSortBy.RecentlyUpdated.ToString()">Last commented</option>
                </select>

                <div class="ms-auto">
                    @* Pagination controls â€” only rendered when there is more than one page *@
                    @if (_threadsPage.TotalPages > 1)
                    {
                        <nav aria-label="Thread pagination">
                            <ul class="pagination pagination-sm mb-0 category-pagination">
                                <li class="page-item @(_pageNumber == 1 ? "disabled" : "")">
                                    <button class="page-link" @onclick="PrevPage">Prev</button>
                                </li>

                                @{
                                    var totalPages = _threadsPage.TotalPages;
                                    var pagesToShow = new SortedSet<int>();
                                    // Always show first and last page, plus neighbours of the current page
                                    pagesToShow.Add(1);
                                    pagesToShow.Add(totalPages);
                                    if (_pageNumber > 1) pagesToShow.Add(_pageNumber - 1);
                                    pagesToShow.Add(_pageNumber);
                                    if (_pageNumber < totalPages) pagesToShow.Add(_pageNumber + 1);

                                    int? lastShown = null;
                                    foreach (var pageNum in pagesToShow)
                                    {
                                        // Insert ellipsis between non-consecutive page numbers
                                        if (lastShown.HasValue && pageNum - lastShown.Value > 1)
                                        {
                                            <li class="page-item disabled">
                                                <span class="page-link">...</span>
                                            </li>
                                        }
                                        <li class="page-item @(pageNum == _pageNumber ? "active" : "")">
                                            <button class="page-link" @onclick="() => GoToPage(pageNum)">@pageNum</button>
                                        </li>
                                        lastShown = pageNum;
                                    }
                                }

                                <li class="page-item @(_pageNumber == _threadsPage.TotalPages ? "disabled" : "")">
                                    <button class="page-link" @onclick="NextPage">Next</button>
                                </li>
                            </ul>
                        </nav>
                    }
                </div>
            </div>

            <div class="autosport-table">
                <table class="threads-table">
                    <thead>
                        <tr>
                            <th>thread</th>
                            <th>author</th>
                            <th>replies</th>
                            <th>created</th>
                            <th>last activity</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var thread in _threadsPage.Items)
                        {
                            <tr class="thread-row">
                                <td class="thread-title-cell">
                                    <a href="/thread/@thread.ThreadId" class="thread-title">@thread.Title</a>
                                </td>
                                <td class="author-cell">@thread.AuthorUserName</td>
                                <td class="replies-cell">@thread.CommentCount</td>
                                <td class="created-cell">
                                    <span class="date-line">@FormatDate(thread.TimeCreated)</span>
                                    <span class="time-line">@FormatTime(thread.TimeCreated)</span>
                                </td>
                                <td class="activity-cell">
                                    @* Last activity is the most recent comment time, or the thread's update/creation time if no comments exist *@
                                    @{ var lastActivity = GetLastActivityDate(thread); }
                                    <span class="date-line">@FormatDate(lastActivity)</span>
                                    <span class="time-line">@FormatTime(lastActivity)</span>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </div>
    }

    @* Authenticated users see a Create Thread button; guests see a login/register prompt *@
    <AuthorizeView>
        <Authorized>
            <div class="create-thread-section">
                <a href="/create-thread/@CategoryName" class="btn btn-primary">
                    Create New Thread
                </a>
            </div>
        </Authorized>
        <NotAuthorized>
            <div class="auth-prompt">
                <span class="auth-prompt-icon">ðŸ’¬</span>
                <p>Want to start a discussion?</p>
                <div class="auth-prompt-links">
                    <a href="/login" class="auth-link">Log in</a>
                    <span>or</span>
                    <a href="/register" class="auth-link">Register</a>
                </div>
            </div>
        </NotAuthorized>
    </AuthorizeView>
</div>

@code {
    [Parameter]
    public string CategoryName { get; set; } = string.Empty; // Bound from the URL segment /category/{CategoryName}

    private PagedResult<ThreadSummaryDto>? _threadsPage; // Null while loading, populated after first fetch
    private int _pageNumber = 1;
    private int _pageSize = 5;
    private int? _categoryId; // Resolved from CategoryName on parameter change; null if category not found

    private ThreadSortBy _sortBy = ThreadSortBy.Newest;
    private string _sortByString => _sortBy.ToString(); // Bound to the sort <select> value

    protected override async Task OnParametersSetAsync()
    {
        // Re-runs whenever the URL category segment changes (e.g. navigating between categories)
        var categories = await CategoryService.GetAllAsync();
        var category = categories?.FirstOrDefault(c =>
            string.Equals(c.Name, CategoryName, StringComparison.OrdinalIgnoreCase));

        _categoryId = category?.CategoryId;
        _pageNumber = 1; // Reset pagination when switching categories
        await LoadThreads();
    }

    private async Task LoadThreads(int? page = null)
    {
        var pageToLoad = page ?? _pageNumber;

        if (_categoryId == null)
        {
            // Category not found â€” render an empty result rather than crashing
            _threadsPage = PagedResult<ThreadSummaryDto>.Create([], 0, 1, _pageSize);
            return;
        }

        var filter = new ThreadFilterParams
        {
            CategoryId = _categoryId,
            PageNumber = pageToLoad,
            PageSize = _pageSize,
            SortBy = _sortBy
        };

        var result = await ThreadService.GetThreadsAsync(filter);
        // Fall back to an empty page if the API returns null
        _threadsPage = result ?? PagedResult<ThreadSummaryDto>.Create([], 0, pageToLoad, _pageSize);
        _pageNumber = _threadsPage.PageNumber;
        StateHasChanged();
    }

    private async Task OnPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == _pageSize) return; // No-op if same size selected
        _pageSize = newSize;
        _pageNumber = 1; // Reset to first page on page size change
        await LoadThreads(_pageNumber);
    }

    private async Task OnSortChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        var val = e.Value.ToString();
        if (Enum.TryParse<ThreadSortBy>(val, out var parsed))
        {
            if (parsed == _sortBy) return; // No-op if same sort selected
            _sortBy = parsed;
            _pageNumber = 1; // Reset to first page on sort change
            await LoadThreads(_pageNumber);
        }
    }

    private async Task PrevPage()
    {
        if (_threadsPage is null || !_threadsPage.HasPreviousPage) return;
        _pageNumber = Math.Max(1, _pageNumber - 1);
        await LoadThreads(_pageNumber);
    }

    private async Task NextPage()
    {
        if (_threadsPage is null || !_threadsPage.HasNextPage) return;
        _pageNumber = Math.Min(_threadsPage.TotalPages, _pageNumber + 1);
        await LoadThreads(_pageNumber);
    }

    private async Task GoToPage(int p)
    {
        if (_threadsPage is null) return;
        if (p < 1 || p > _threadsPage.TotalPages) return;
        _pageNumber = p;
        await LoadThreads(_pageNumber);
    }

    // Formats a DateTime to a readable date string, e.g. "Mar 11, 2026"
    private static string FormatDate(DateTime dt) => dt.ToString("MMM dd, yyyy");

    // Formats a DateTime to a 24-hour time string, e.g. "14:35"
    private static string FormatTime(DateTime dt) => dt.ToString("HH:mm");

    private static DateTime GetLastActivityDate(ThreadSummaryDto t)
    {
        // Prefer the most recent comment time; fall back to update time, then creation time
        if (t.LastCommentTime.HasValue)
            return t.LastCommentTime.Value;
        return t.TimeUpdated != default ? t.TimeUpdated : t.TimeCreated;
    }
}```
## File: \src\Forum.Blazor\Components\Pages\CreateThread.razor
```razor
@page "/create-thread"
@page "/create-thread/{CategoryName}"           
@inject CategoryService CategoryService        
@inject ThreadService ThreadService             
@inject NavigationManager NavigationManager     

<PageTitle>Create New Thread</PageTitle>

<link rel="stylesheet" href="/css/create-thread.css" />

<AuthorizeView>
<NotAuthorized>
    @* Unauthenticated users are shown a prompt rather than the form *@
    <div class="row justify-content-center mt-5">
        <div class="col-md-6 text-center">
            <h3>Please log in</h3>
            <p>You must be logged in to create a thread.</p>
            <a href="/login" class="btn btn-primary">Log In</a>
        </div>
    </div>
</NotAuthorized>
<Authorized Context="authContext">

<div class="create-thread-container">
    <div class="form-header">
        <h1>Create New Thread</h1>
        <p>start a new discussion in the forum.</p>
    </div>

    @* Show a spinner-like message while the API call is in flight *@
    @if (_isSubmitting)
    {
        <div class="submitting">creating thread...</div>
    }
    else
    {
        @* EditForm uses DataAnnotations on CreateThreadModel for client-side validation *@
        <EditForm Model="@_newThread" OnValidSubmit="@HandleSubmit">
            <DataAnnotationsValidator />
            <ValidationSummary />

            <div class="form-group">
                <label for="title">title</label>
                <InputText id="title" @bind-Value="@_newThread.Title" class="form-control" />
                <ValidationMessage For="@(() => _newThread.Title)" />
            </div>

            <div class="form-group">
                <label>category</label>

                @* Category is pre-selected from the URL and shown as a read-only display input *@
               @if (_categories != null)
                        {
                   var selected = _categories.FirstOrDefault(c => c.CategoryId == _newThread.CategoryId);

                    <input class="form-control category-display" value="@selected?.Name" disabled />
                }
            </div>

            <div class="form-group">
                <label for="body">message</label>
                <InputTextArea id="body" @bind-Value="@_newThread.Body" class="form-control" rows="10" />
                <ValidationMessage For="@(() => _newThread.Body)" />
            </div>

            <div class="form-actions">
                <button type="submit" class="btn btn-primary" disabled="@_isSubmitting">
                    create thread
                </button>
                @* Cancel navigates back to the home page without submitting *@
                <button type="button" class="btn btn-secondary" @onclick="@(() => NavigationManager.NavigateTo("/"))">
                    cancel
                </button>
            </div>
        </EditForm>
    }

    @* Displays API or network errors below the form *@
    @if (!string.IsNullOrEmpty(_errorMessage))
    {
        <div class="error-message">@_errorMessage</div>
    }
</div>

</Authorized>
</AuthorizeView>

@code {
    [Parameter]
    public string? CategoryName { get; set; } // Optional URL segment; pre-selects the category if provided

    private CreateThreadModel _newThread = new();
    private List<CategoryDto>? _categories;  // All available categories, loaded on init
    private bool _isSubmitting;              // Prevents double-submit while API call is in flight
    private string _errorMessage = string.Empty;

    // Local form model with validation attributes â€” keeps API DTOs clean
    public class CreateThreadModel
    {
        [Required(ErrorMessage = "title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "title must be between 3 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "category is required")]
        public string CategoryIdString { get; set; } = string.Empty;

        // Parsed convenience property â€” avoids storing an int that can't bind to a select easily
        public int CategoryId => string.IsNullOrEmpty(CategoryIdString) ? 0 : int.Parse(CategoryIdString);

        [Required(ErrorMessage = "message is required")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "message must be between 10 and 2000 characters")]
        public string Body { get; set; } = string.Empty;

        // Maps the form model to the API DTO
        public CreateThreadDto ToDto() => new(Title, CategoryId, Body);
    }

    protected override async Task OnInitializedAsync()
    {
        var categories = await CategoryService.GetAllAsync();
        _categories = categories?.ToList();

        // If a category name was passed in the URL, pre-select it in the form
        if (!string.IsNullOrEmpty(CategoryName) && _categories != null)
        {
            var selectedCategory = _categories
                .FirstOrDefault(c => c.Name.Equals(CategoryName, StringComparison.OrdinalIgnoreCase));

            if (selectedCategory != null)
            {
                _newThread.CategoryIdString = selectedCategory.CategoryId.ToString();
            }
        }
    }

    private async Task HandleSubmit()
    {
        if (_categories == null) return;

        _isSubmitting = true;
        _errorMessage = string.Empty;

        try
        {
            var response = await ThreadService.CreateAsync(_newThread.ToDto());

            if (response.IsSuccessStatusCode)
            {
                // On success, redirect to the category page the thread was posted in
                var selectedCategory = _categories.FirstOrDefault(c => c.CategoryId == _newThread.CategoryId);
                if (selectedCategory != null)
                {
                    NavigationManager.NavigateTo($"/category/{selectedCategory.Name}");
                }
                else
                {
                    // Fallback if category can't be resolved
                    NavigationManager.NavigateTo("/threads");
                }
            }
            else
            {
                _errorMessage = "failed to create thread. please try again.";
            }
        }
        catch
        {
            // Catches network failures or unexpected exceptions
            _errorMessage = "an error occurred while creating the thread.";
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
```
## File: \src\Forum.Blazor\Components\Pages\Error.razor
```razor
@page "/Error"
@using System.Diagnostics
<PageTitle>Error</PageTitle>
<h1 class="text-danger">Error.</h1>
<h2 class="text-danger">An error occurred while processing your request.</h2>
@* Show the request ID if available — useful for correlating with server logs *@
@if (ShowRequestId)
{
    <p>
        <strong>Request ID:</strong> <code>@RequestId</code>
    </p>
}
<h3>Development Mode</h3>
<p>
    Swapping to <strong>Development</strong> environment will display more detailed information about the error that occurred.
</p>
<p>
    <strong>The Development environment shouldn't be enabled for deployed applications.</strong>
    It can result in displaying sensitive information from exceptions to end users.
    For local debugging, enable the <strong>Development</strong> environment by setting the <strong>ASPNETCORE_ENVIRONMENT</strong> environment variable to <strong>Development</strong>
    and restarting the app.
</p>
@code{
    [CascadingParameter] private HttpContext? HttpContext { get; set; } // Provides access to the current HTTP context for trace info
    private string? RequestId { get; set; }                             // The trace/request ID displayed on the error page
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);     // Only show the request ID section if one is available
    protected override void OnInitialized() =>
        RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier; // Prefer the diagnostic activity ID, fall back to the HTTP trace identifier
}```
## File: \src\Forum.Blazor\Components\Pages\Home.razor
```razor
@page "/"
@inject CategoryService CategoryService   
@inject ThreadService ThreadService       

<PageTitle>Overtime</PageTitle>

<link rel="stylesheet" href="/css/home.css" />

<div class="home-wrapper">
    <div class="home-overlay">

        <div class="home-content">

            @* Show a loading indicator while threads are being fetched *@
            @if (_popularThreads == null)
            {
                <div class="loading">loading...</div>
            }
            else
            {
                @* Popular Threads section â€” top 5 threads ranked by comment count *@
                @if (_popularThreads.Any())
                {
                    <div class="popular-section">
                        <div class="section-title">Popular Threads</div>
                        <div class="popular-list">
                            @foreach (var thread in _popularThreads)
                            {
                                <a href="/thread/@thread.ThreadId" class="popular-item">
                                    <span class="popular-title">@thread.Title</span>
                                    <span class="popular-meta">
                                        <span class="author">@thread.AuthorUserName</span>
                                        <span class="dot">&bull;</span>
                                        <span class="popular-category">@thread.CategoryName</span>
                                    </span>
                                    <span class="popular-replies">@thread.CommentCount replies</span>
                                </a>
                            }
                        </div>
                    </div>
                }

                @* Popular Categories section â€” top 4 categories ranked by most recent activity *@
                @if (_popularCategories != null && _popularCategories.Any())
                {
                    <div class="categories-section">
                        <div class="section-title">Popular Categories</div>
                        <div class="categories-grid">
                            @foreach (var category in _popularCategories)
                            {
                                <div class="category-card">
                                    <div class="category-header">
                                        <a href="/category/@category.Name" class="cat-name">@category.Name</a>
                                        <span class="cat-count">@category.ThreadCount threads</span>
                                        <a href="/category/@category.Name" class="view-all-link">View all &rarr;</a>
                                    </div>

                                    @* Show up to 2 recent threads per category card, or a placeholder if empty *@
                                    @if (category.RecentThreads == null || !category.RecentThreads.Any())
                                    {
                                        <div class="no-threads">no threads yet.</div>
                                    }
                                    else
                                    {
                                        <div class="recent-threads">
                                            @foreach (var thread in category.RecentThreads)
                                            {
                                                <div class="thread-item">
                                                    <div class="thread-left">
                                                        <a href="/thread/@thread.ThreadId" class="thread-title">@thread.Title</a>
                                                        <div class="thread-meta">
                                                            <span class="author">@thread.AuthorUserName</span>
                                                            <span class="dot">&bull;</span>
                                                            <span class="date">@thread.TimeCreated.ToString("MMM dd, yyyy")</span>
                                                        </div>
                                                    </div>
                                                    <div class="thread-right">
                                                        <span class="reply-count">@thread.CommentCount</span>
                                                        <span class="reply-label">replies</span>
                                                    </div>
                                                </div>
                                            }
                                        </div>
                                    }
                                </div>
                            }
                        </div>
                    </div>
                }
            }

        </div>
    </div>
</div>

@code {
    private List<PopularThreadItem>? _popularThreads;       // Null while loading; top 5 threads by comment count
    private List<CategoryWithThreads>? _popularCategories; // Top 4 categories by most recent activity

    // Local view model combining category metadata with its recent threads
    private class CategoryWithThreads
    {
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int ThreadCount { get; set; }
        public DateTime LatestActivity { get; set; }         // Used to rank categories by recent activity
        public List<ThreadSummaryDto> RecentThreads { get; set; } = [];
    }

    // Local view model for displaying a thread across categories on the home page
    private class PopularThreadItem
    {
        public int ThreadId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AuthorUserName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int CommentCount { get; set; }
    }

    protected override async Task OnInitializedAsync()
    {
        var categories = await CategoryService.GetAllAsync();
        if (categories == null) return;

        var allCategories = new List<CategoryWithThreads>();
        _popularThreads = [];

        foreach (var cat in categories)
        {
            // Fetch the 3 most recently updated threads per category
            var filter = new ThreadFilterParams
            {
                CategoryId = cat.CategoryId,
                PageSize = 3,
                SortBy = ThreadSortBy.RecentlyUpdated
            };
            var result = await ThreadService.GetThreadsAsync(filter);
            var threads = result?.Items?.ToList() ?? [];

            // Use latest comment/update time to rank categories by recent activity
            var latestActivity = threads
                .Select(t => t.LastCommentTime ?? t.TimeUpdated)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            allCategories.Add(new CategoryWithThreads
            {
                Name = cat.Name,
                CategoryId = cat.CategoryId,
                ThreadCount = cat.ThreadCount,
                LatestActivity = latestActivity,
                RecentThreads = threads.Take(2).ToList() // Show at most 2 threads per category card
            });

            // Collect all fetched threads into the global popular list for cross-category ranking
            foreach (var t in threads)
            {
                _popularThreads.Add(new PopularThreadItem
                {
                    ThreadId = t.ThreadId,
                    Title = t.Title,
                    AuthorUserName = t.AuthorUserName,
                    CategoryName = cat.Name,
                    CommentCount = t.CommentCount
                });
            }
        }

        // Pick the top 5 threads across all categories, ranked by comment count
        _popularThreads = _popularThreads
            .OrderByDescending(t => t.CommentCount)
            .Take(5)
            .ToList();

        // Show the 4 most recently active categories
        _popularCategories = allCategories
            .OrderByDescending(c => c.LatestActivity)
            .Take(4)
            .ToList();
    }
}```
## File: \src\Forum.Blazor\Components\Pages\Login.razor
```razor
@page "/login"
@using Forum.Blazor.Models
@inject IAuthClientService AuthService  
@inject NavigationManager Navigation    
@inject IJSRuntime JS                   

<PageTitle>Login</PageTitle>
<link rel="stylesheet" href="/css/auth.css" />

<div class="auth-container">
    <div class="auth-card">
        <div class="auth-card-header">
            <h3>Login</h3>
        </div>
        <div class="auth-card-body">

            @* Display login error (wrong credentials, network failure, etc.) *@
            @if (!string.IsNullOrEmpty(errorMessage))
            {
                <div class="alert alert-danger">
                    @errorMessage
                </div>
            }

            <EditForm Model="@loginModel" OnValidSubmit="HandleLogin">

                <div class="mb-3">
                    <label class="form-label">Username</label>
                    <InputText class="form-control" @bind-Value="loginModel.Username" />
                </div>

                <div class="mb-3">
                    <label class="form-label">Password</label>
                    <InputText type="password" class="form-control" @bind-Value="loginModel.Password" />
                </div>

                @* Button is disabled while the API call is in flight to prevent double-submit *@
                <button type="submit" class="btn btn-primary" disabled="@isLoading">
                    @* Show a spinner inside the button while loading *@
                    @if (isLoading)
                    {
                        <span class="spinner-border spinner-border-sm me-2"></span>
                    }
                    Login
                </button>

                <a href="/register" class="auth-link">Don't have an account? Register</a>

            </EditForm>
        </div>
    </div>
</div>

@code {

    private LoginRequest loginModel = new();
    private string errorMessage = string.Empty;
    private bool isLoading = false; // Prevents double-submit while the login request is in flight

    private async Task HandleLogin()
    {
        errorMessage = string.Empty;

        // Basic client-side guard before hitting the API
        if (string.IsNullOrWhiteSpace(loginModel.Username))
        {
            errorMessage = "Enter a username.";
            return;
        }

        isLoading = true;

        try
        {
            var result = await AuthService.LoginAsync(loginModel);

            if (result.Succeeded)
            {
                // Redirect to home page on successful login
                Navigation.NavigateTo("/");
            }
            else
            {
                // Show the API error message, or a generic fallback
                errorMessage = result.ErrorMessage ?? "Invalid username or password.";
            }
        }
        catch (Exception ex)
        {
            // Catches network failures or unexpected exceptions
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

}```
## File: \src\Forum.Blazor\Components\Pages\Logout.razor
```razor
@page "/logout"
@using Forum.Blazor.Services
@inject IAuthClientService AuthService
@inject NavigationManager Navigation

<PageTitle>Logging out...</PageTitle>

<div class="row justify-content-center">
    <div class="col-md-6 text-center">
        <p>Logging out...</p>
    </div>
</div>

@code {
    protected override async Task OnInitializedAsync()
    {
        // Clear the auth session then redirect to home
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("/");
    }
}```
## File: \src\Forum.Blazor\Components\Pages\Profile.razor
```razor
@page "/profile"
@using System.Security.Claims
@using Forum.Application.DTOs.Thread
@using Forum.Application.DTOs.Comment
@inject UserService UserService
@inject ThreadService ThreadService
@inject CommentService CommentService
@inject NavigationManager Navigation
@inject AuthenticationStateProvider AuthStateProvider

<PageTitle>Profile</PageTitle>

<link rel="stylesheet" href="/css/panel.css" />

<div class="panel-wrapper">
    <nav class="breadcrumb-trail">
        SPORT FORUM > <strong>PROFILE</strong>
    </nav>

    <h1 class="panel-title">Profile</h1>

    @* Tab buttons â€” My Threads and My Comments load lazily on first click *@
    <div class="panel-tabs">
        <button class="panel-tab @(activeTab == "account" ? "active" : "")" @onclick='() => activeTab = "account"'>Account Settings</button>
        <button class="panel-tab @(activeTab == "mythreads" ? "active" : "")" @onclick="ActivateMyThreads">My Threads</button>
        <button class="panel-tab @(activeTab == "mycomments" ? "active" : "")" @onclick="ActivateMyComments">My Comments</button>
    </div>

    @* Status banners â€” shown after profile update operations *@
    @if (!string.IsNullOrEmpty(successMessage))
    {
        <div class="panel-status success">@successMessage</div>
    }

    @if (!string.IsNullOrEmpty(errorMessage))
    {
        <div class="panel-status error">@errorMessage</div>
    }

    @* ===== ACCOUNT SETTINGS TAB ===== *@
    @if (activeTab == "account")
    {
        <div class="panel-section panel-section-narrow">
            @* Show spinner while profile data is loading from the API *@
            @if (isLoadingProfile)
            {
                <div class="text-center">
                    <div class="spinner-border"></div>
                </div>
            }
            else
            {
                @* Username is read-only â€” can only be changed by an admin *@
                <div class="mb-4">
                    <label class="form-label fw-bold">Username</label>
                    <input class="form-control" value="@profileModel.Username" disabled />
                </div>

                <hr />

                <EditForm Model="@emailModel" OnValidSubmit="HandleUpdateEmail">
                    <h5 class="mt-3">Change Email</h5>

                    <div class="mb-3">
                        <label class="form-label">New Email</label>
                        <InputText type="email"
                                   class="form-control"
                                   @bind-Value="emailModel.NewEmail" />
                    </div>

                    @* Button disabled while API call is in flight *@
                    <button type="submit" class="btn btn-primary" disabled="@isLoading">
                        @if (isLoading)
                        {
                            <span class="spinner-border spinner-border-sm me-2"></span>
                        }
                        Update Email
                    </button>
                </EditForm>

                <hr class="my-4" />

                <EditForm Model="@passwordModel" OnValidSubmit="HandleChangePassword">
                    <h5>Change Password</h5>

                    <div class="mb-3">
                        <label class="form-label">Current Password</label>
                        <InputText type="password"
                                   class="form-control"
                                   @bind-Value="passwordModel.CurrentPassword" />
                    </div>

                    <div class="mb-3">
                        <label class="form-label">New Password</label>
                        <InputText type="password"
                                   class="form-control"
                                   @bind-Value="passwordModel.NewPassword" />
                    </div>

                    <div class="mb-3">
                        <label class="form-label">Confirm New Password</label>
                        <InputText type="password"
                                   class="form-control"
                                   @bind-Value="passwordModel.ConfirmNewPassword" />
                    </div>

                    @* Button disabled while API call is in flight *@
                    <button type="submit" class="btn btn-warning" disabled="@isLoading">
                        @if (isLoading)
                        {
                            <span class="spinner-border spinner-border-sm me-2"></span>
                        }
                        Change Password
                    </button>
                </EditForm>
            }
        </div>
    }

    @* ===== MY THREADS TAB ===== *@
    @if (activeTab == "mythreads")
    {
        <div class="panel-section">
            <h2>My Threads</h2>

            <div class="d-flex align-items-center mb-2">
                @* Page size selector â€” resets to page 1 on change *@
                <label class="me-2 mb-0" style="color: #8b949e;">Threads per page:</label>
                <select class="form-select form-select-sm w-auto panel-pagination-select" value="@myThreadsPageSize" @onchange="OnMyThreadsPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                @* Sort selector â€” resets to page 1 on change *@
                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_myThreadsSortByString" @onchange="OnMyThreadsSortChanged">
                    <option value="@ThreadSortBy.Newest.ToString()">Newest</option>
                    <option value="@ThreadSortBy.Oldest.ToString()">Oldest</option>
                    <option value="@ThreadSortBy.Category.ToString()">Category</option>
                </select>

                <div class="ms-auto">
                    @* Pagination controls â€” only rendered when there is more than one page *@
                    @if (myThreadsPaged != null && myThreadsPaged.TotalPages > 1)
                    {
                        <nav aria-label="My threads pagination">
                            <ul class="pagination pagination-sm mb-0 panel-pagination-nav">
                                <li class="page-item @(myThreadsPage == 1 ? "disabled" : "")">
                                    <button class="page-link" @onclick="PrevMyThreadsPage">Prev</button>
                                </li>

                                @{
                                    var ttPages = myThreadsPaged.TotalPages;
                                    var ttShow = new SortedSet<int>();
                                    // Always show first and last page, plus neighbours of the current page
                                    ttShow.Add(1);
                                    ttShow.Add(ttPages);
                                    if (myThreadsPage > 1) ttShow.Add(myThreadsPage - 1);
                                    ttShow.Add(myThreadsPage);
                                    if (myThreadsPage < ttPages) ttShow.Add(myThreadsPage + 1);

                                    int? ttLast = null;
                                    foreach (var pageNum in ttShow)
                                    {
                                        // Insert ellipsis between non-consecutive page numbers
                                        if (ttLast.HasValue && pageNum - ttLast.Value > 1)
                                        {
                                            <li class="page-item disabled">
                                                <span class="page-link">...</span>
                                            </li>
                                        }
                                        <li class="page-item @(pageNum == myThreadsPage ? "active" : "")">
                                            <button class="page-link" @onclick="() => GoToMyThreadsPage(pageNum)">@pageNum</button>
                                        </li>
                                        ttLast = pageNum;
                                    }
                                }

                                <li class="page-item @(myThreadsPage == myThreadsPaged.TotalPages ? "disabled" : "")">
                                    <button class="page-link" @onclick="NextMyThreadsPage">Next</button>
                                </li>
                            </ul>
                        </nav>
                    }
                </div>
            </div>

            <div class="panel-table-wrap">
            <table class="panel-table">
                <thead>
                    <tr>
                        <th>Title</th>
                        <th>Category</th>
                        <th>Comments</th>
                        <th>Created</th>
                    </tr>
                </thead>
                <tbody>
                    @if (myThreads != null)
                    {
                        @foreach (var t in myThreads)
                        {
                            <tr>
                                <td><a href="/thread/@t.ThreadId">@t.Title</a></td>
                                <td>@t.CategoryName</td>
                                <td>@t.CommentCount</td>
                                <td>@t.TimeCreated.ToString("yyyy-MM-dd HH:mm")</td>
                            </tr>
                        }
                    }
                </tbody>
            </table>
            </div>
        </div>
    }

    @* ===== MY COMMENTS TAB ===== *@
    @if (activeTab == "mycomments")
    {
        <div class="panel-section">
            <h2>My Comments</h2>

            <div class="d-flex align-items-center mb-2">
                @* Page size selector â€” resets to page 1 on change *@
                <label class="me-2 mb-0" style="color: #8b949e;">Comments per page:</label>
                <select class="form-select form-select-sm w-auto panel-pagination-select" value="@myCommentsPageSize" @onchange="OnMyCommentsPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                @* Sort selector â€” resets to page 1 on change *@
                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_myCommentsSortByString" @onchange="OnMyCommentsSortChanged">
                    <option value="@CommentSortBy.Newest.ToString()">Newest</option>
                    <option value="@CommentSortBy.Oldest.ToString()">Oldest</option>
                </select>

                <div class="ms-auto">
                    @* Pagination controls â€” only rendered when there is more than one page *@
                    @if (myCommentsPaged != null && myCommentsPaged.TotalPages > 1)
                    {
                        <nav aria-label="My comments pagination">
                            <ul class="pagination pagination-sm mb-0 panel-pagination-nav">
                                <li class="page-item @(myCommentsPage == 1 ? "disabled" : "")">
                                    <button class="page-link" @onclick="PrevMyCommentsPage">Prev</button>
                                </li>

                                @{
                                    var ccPages = myCommentsPaged.TotalPages;
                                    var ccShow = new SortedSet<int>();
                                    // Always show first and last page, plus neighbours of the current page
                                    ccShow.Add(1);
                                    ccShow.Add(ccPages);
                                    if (myCommentsPage > 1) ccShow.Add(myCommentsPage - 1);
                                    ccShow.Add(myCommentsPage);
                                    if (myCommentsPage < ccPages) ccShow.Add(myCommentsPage + 1);

                                    int? ccLast = null;
                                    foreach (var pageNum in ccShow)
                                    {
                                        // Insert ellipsis between non-consecutive page numbers
                                        if (ccLast.HasValue && pageNum - ccLast.Value > 1)
                                        {
                                            <li class="page-item disabled">
                                                <span class="page-link">...</span>
                                            </li>
                                        }
                                        <li class="page-item @(pageNum == myCommentsPage ? "active" : "")">
                                            <button class="page-link" @onclick="() => GoToMyCommentsPage(pageNum)">@pageNum</button>
                                        </li>
                                        ccLast = pageNum;
                                    }
                                }

                                <li class="page-item @(myCommentsPage == myCommentsPaged.TotalPages ? "disabled" : "")">
                                    <button class="page-link" @onclick="NextMyCommentsPage">Next</button>
                                </li>
                            </ul>
                        </nav>
                    }
                </div>
            </div>

            <div class="panel-table-wrap">
            <table class="panel-table">
                <thead>
                    <tr>
                        <th>Content</th>
                        <th>Thread</th>
                        <th>Created</th>
                    </tr>
                </thead>
                <tbody>
                    @if (myComments != null)
                    {
                        @foreach (var c in myComments)
                        {
                            <tr>
                                @* Full content shown in the title tooltip on hover *@
                                <td><a href="/thread/@c.ThreadId" class="comment-preview" title="@c.Content">@c.Content</a></td>
                                <td>@(c.ThreadTitle ?? "Unknown")</td>
                                <td>@c.TimeCreated.ToString("yyyy-MM-dd HH:mm")</td>
                            </tr>
                        }
                    }
                </tbody>
            </table>
            </div>
        </div>
    }
</div>

@code {
    private string activeTab = "account"; // Currently active tab: "account", "mythreads", or "mycomments"

    private ProfileModel profileModel = new();
    private UpdateEmailModel emailModel = new();
    private ChangePasswordModel passwordModel = new();

    private string errorMessage = string.Empty;
    private string successMessage = string.Empty;

    private bool isLoading = false;        // Prevents double-submit during API calls
    private bool isLoadingProfile = true;  // Shows spinner until profile data is fetched

    private string? userId; // Resolved from auth claims on init; used for all API calls

    // My Threads
    private List<ThreadSummaryDto> myThreads = [];
    private PagedResult<ThreadSummaryDto>? myThreadsPaged;
    private int myThreadsPage = 1;
    private int myThreadsPageSize = 5;
    private bool myThreadsLoaded;  // Lazy-load guard â€” data only fetched on first tab activation
    private ThreadSortBy _myThreadsSortBy = ThreadSortBy.Newest;
    private string _myThreadsSortByString => _myThreadsSortBy.ToString(); // Bound to the sort <select> value

    // My Comments
    private List<CommentDto> myComments = [];
    private PagedResult<CommentDto>? myCommentsPaged;
    private int myCommentsPage = 1;
    private int myCommentsPageSize = 5;
    private bool myCommentsLoaded;  // Lazy-load guard â€” data only fetched on first tab activation
    private CommentSortBy _myCommentsSortBy = CommentSortBy.Newest;
    private string _myCommentsSortByString => _myCommentsSortBy.ToString(); // Bound to the sort <select> value

    protected override async Task OnInitializedAsync()
    {
        // Resolve the current user's ID from authentication claims
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Redirect to login if the user is not authenticated
        if (string.IsNullOrEmpty(userId))
        {
            Navigation.NavigateTo("/login");
            return;
        }

        try
        {
            var profile = await UserService.GetProfileAsync(userId);

            if (profile != null)
            {
                profileModel.Username = profile.UserName;
                profileModel.Email = profile.Email ?? string.Empty;
                emailModel.NewEmail = profile.Email ?? string.Empty; // Pre-fill the email field
            }
            else
            {
                errorMessage = "Failed to load profile.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isLoadingProfile = false;
        }
    }

    private async Task HandleUpdateEmail()
    {
        errorMessage = string.Empty;
        successMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(emailModel.NewEmail))
        {
            errorMessage = "Email cannot be empty.";
            return;
        }

        isLoading = true;

        try
        {
            var response = await UserService.UpdateAsync(userId!, new UpdateUserProfileDto(Email: emailModel.NewEmail));

            if (response.IsSuccessStatusCode)
            {
                successMessage = "Email updated successfully.";
                profileModel.Email = emailModel.NewEmail; // Sync local model with the saved value
            }
            else
            {
                // Try to extract a readable error from the API response body
                var body = await response.Content.ReadAsStringAsync();
                errorMessage = ApiClientBase.TryExtractError(body) ?? "Failed to update email.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task HandleChangePassword()
    {
        errorMessage = string.Empty;
        successMessage = string.Empty;

        // Client-side validation before hitting the API
        if (string.IsNullOrWhiteSpace(passwordModel.CurrentPassword))
        {
            errorMessage = "Current password is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordModel.NewPassword))
        {
            errorMessage = "New password cannot be empty.";
            return;
        }

        if (passwordModel.NewPassword != passwordModel.ConfirmNewPassword)
        {
            errorMessage = "New password and confirmation do not match.";
            return;
        }

        isLoading = true;

        try
        {
            var response = await UserService.ChangePasswordAsync(userId!, passwordModel.CurrentPassword, passwordModel.NewPassword);

            if (response.IsSuccessStatusCode)
            {
                successMessage = "Password changed successfully.";
                passwordModel = new ChangePasswordModel(); // Clear the form on success
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                errorMessage = ApiClientBase.TryExtractError(body) ?? "Failed to change password.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    // ===== My Threads =====
    private async Task ActivateMyThreads()
    {
        activeTab = "mythreads";
        // Only load data on the first activation to avoid redundant API calls
        if (!myThreadsLoaded)
        {
            await LoadMyThreads();
            myThreadsLoaded = true;
        }
    }

    private async Task LoadMyThreads()
    {
        if (userId == null) return;
        myThreadsPaged = await ThreadService.GetThreadsAsync(new ThreadFilterParams
        {
            AuthorId = userId,
            SortBy = _myThreadsSortBy,
            PageNumber = myThreadsPage,
            PageSize = myThreadsPageSize
        });
        myThreads = myThreadsPaged?.Items?.ToList() ?? [];
    }

    private async Task OnMyThreadsSortChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!Enum.TryParse<ThreadSortBy>(e.Value.ToString(), out var parsed)) return;
        if (parsed == _myThreadsSortBy) return; // No-op if same sort selected
        _myThreadsSortBy = parsed;
        myThreadsPage = 1; // Reset to first page on sort change
        await LoadMyThreads();
    }

    private async Task OnMyThreadsPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == myThreadsPageSize) return; // No-op if same size selected
        myThreadsPageSize = newSize;
        myThreadsPage = 1; // Reset to first page on page size change
        await LoadMyThreads();
    }

    private async Task PrevMyThreadsPage()
    {
        if (myThreadsPage > 1) { myThreadsPage--; await LoadMyThreads(); }
    }

    private async Task NextMyThreadsPage()
    {
        if (myThreadsPaged != null && myThreadsPage < myThreadsPaged.TotalPages) { myThreadsPage++; await LoadMyThreads(); }
    }

    private async Task GoToMyThreadsPage(int p)
    {
        if (myThreadsPaged == null || p < 1 || p > myThreadsPaged.TotalPages) return;
        myThreadsPage = p;
        await LoadMyThreads();
    }

    // ===== My Comments =====
    private async Task ActivateMyComments()
    {
        activeTab = "mycomments";
        // Only load data on the first activation to avoid redundant API calls
        if (!myCommentsLoaded)
        {
            await LoadMyComments();
            myCommentsLoaded = true;
        }
    }

    private async Task LoadMyComments()
    {
        if (userId == null) return;
        myCommentsPaged = await CommentService.GetByAuthorAsync(userId, myCommentsPage, myCommentsPageSize, _myCommentsSortBy.ToString());
        myComments = myCommentsPaged?.Items?.ToList() ?? [];
    }

    private async Task OnMyCommentsSortChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!Enum.TryParse<CommentSortBy>(e.Value.ToString(), out var parsed)) return;
        if (parsed == _myCommentsSortBy) return; // No-op if same sort selected
        _myCommentsSortBy = parsed;
        myCommentsPage = 1; // Reset to first page on sort change
        await LoadMyComments();
    }

    private async Task OnMyCommentsPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == myCommentsPageSize) return; // No-op if same size selected
        myCommentsPageSize = newSize;
        myCommentsPage = 1; // Reset to first page on page size change
        await LoadMyComments();
    }

    private async Task PrevMyCommentsPage()
    {
        if (myCommentsPage > 1) { myCommentsPage--; await LoadMyComments(); }
    }

    private async Task NextMyCommentsPage()
    {
        if (myCommentsPaged != null && myCommentsPage < myCommentsPaged.TotalPages) { myCommentsPage++; await LoadMyComments(); }
    }

    private async Task GoToMyCommentsPage(int p)
    {
        if (myCommentsPaged == null || p < 1 || p > myCommentsPaged.TotalPages) return;
        myCommentsPage = p;
        await LoadMyComments();
    }

    // Local form models â€” keep API DTOs clean and allow profile-specific validation
    private class ProfileModel
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    private class UpdateEmailModel
    {
        public string NewEmail { get; set; } = string.Empty;
    }

    private class ChangePasswordModel
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}```
## File: \src\Forum.Blazor\Components\Pages\Register.razor
```razor
@page "/register"
@using Forum.Blazor.Models
@inject IAuthClientService AuthService
@inject NavigationManager Navigation
@implements IDisposable

<PageTitle>Register</PageTitle>
<link rel="stylesheet" href="/css/auth.css" />

<div class="auth-container">
    <div class="auth-card">
        <div class="auth-card-header">
            <h3>Register</h3>
        </div>
        <div class="auth-card-body">

            @* Display registration error (validation failure, duplicate username, etc.) *@
            @if (!string.IsNullOrEmpty(errorMessage))
            {
                <div class="alert alert-danger">
                    @errorMessage
                </div>
            }

            @* Shown briefly after successful registration before redirecting to login *@
            @if (registrationSuccessful)
            {
                <div class="alert alert-success">
                    Registration successful! Redirecting to login...
                </div>
            }

            <EditForm Model="@registerModel" OnValidSubmit="HandleRegister">

                <div class="mb-3">
                    <label class="form-label">Username</label>
                    <InputText class="form-control" @bind-Value="registerModel.Username" />
                </div>

                <div class="mb-3">
                    <label class="form-label">Email</label>
                    <InputText type="email" class="form-control" @bind-Value="registerModel.Email" />
                </div>

                <div class="mb-3">
                    <label class="form-label">Password</label>
                    <InputText type="password" class="form-control" @bind-Value="registerModel.Password" />
                </div>

                <div class="mb-3">
                    <label class="form-label">Confirm Password</label>
                    <InputText type="password" class="form-control" @bind-Value="registerModel.ConfirmPassword" />
                </div>

                @* Button disabled while the API call is in flight to prevent double-submit *@
                <button type="submit" class="btn btn-primary" disabled="@isLoading">
                    @if (isLoading)
                    {
                        <span class="spinner-border spinner-border-sm me-2"></span>
                    }
                    Register
                </button>

                <a href="/login" class="auth-link">Already have an account? Login</a>

            </EditForm>
        </div>
    </div>
</div>

@code {

    private CancellationTokenSource _cts = new(); // Used to cancel the redirect delay if the component is disposed early
    private RegisterRequest registerModel = new();
    private string errorMessage = string.Empty;
    private bool isLoading = false;           // Prevents double-submit while the API call is in flight
    private bool registrationSuccessful = false;

    /// <summary>
    /// Handles registration form submission.
    /// Validates password confirmation, calls the client auth service, and navigates to login on success.
    /// </summary>
    private async Task HandleRegister()
    {
        errorMessage = string.Empty;
        isLoading = true;

        try
        {
            // Validate password confirmation on the client before calling API
            if (registerModel.Password != registerModel.ConfirmPassword)
            {
                errorMessage = "Passwords do not match.";
                return;
            }

            var result = await AuthService.RegisterAsync(registerModel);

            if (result.Succeeded)
            {
                registrationSuccessful = true;
                StateHasChanged();
                // Brief delay so the user sees the success message before redirect
                await Task.Delay(2000, _cts.Token);
                Navigation.NavigateTo("/login");
            }
            else
            {
                errorMessage = result.ErrorMessage ?? "Registration failed.";
            }
        }
        catch (OperationCanceledException)
        {
            // Swallowed intentionally â€” triggered when the user navigates away before the delay completes
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    // Cancel the redirect delay if the user navigates away early
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

}```
## File: \src\Forum.Blazor\Components\Pages\ThreadDetails.razor
```razor
@page "/thread/{Id:int}"
@inject ThreadService ThreadService
@inject CommentService CommentService
@inject IJSRuntime JS

<link rel="stylesheet" href="/css/thread-details.css" />

<PageTitle>Thread Details - Overtime</PageTitle>

@* Loading / not-found / content states *@
@if (_isLoading)
{
    <div class="loading">loading thread...</div>
}
else if (_thread == null)
{
    <div class="error">thread not found.</div>
}
else
{
    <div class="thread-details">
        <nav class="breadcrumb-trail">
            <a href="/">Overtime</a>
            <span> &rsaquo; </span>
            <a href="/category/@_thread.CategoryName">@_thread.CategoryName.ToUpper()</a>
            <span> &rsaquo; </span>
            <span class="breadcrumb-current">@_thread.Title</span>
        </nav>

        <div class="thread-header">
            <h1>@_thread.Title</h1>
            <div class="thread-meta">
                <span>started by <strong>@_thread.AuthorUserName</strong></span>
                <span>@_thread.TimeCreated.ToString("MMM dd, yyyy")</span>
                <span>in <strong>@_thread.CategoryName</strong></span>
            </div>
        </div>

        @* The thread body is stored as a special root comment on the thread *@
        @if (_thread.BodyComment != null && !string.IsNullOrEmpty(_thread.BodyComment.Content))
        {
            <div class="body-comment">
                <div class="comment-content">@_thread.BodyComment.Content</div>
            </div>
        }

        <div class="comments-section">
            <h3>comments (@(_commentsPage?.TotalCount ?? 0))</h3>

            <div class="d-flex align-items-center mb-2">
                @* Page size selector â€” resets to page 1 on change *@
                <label class="me-2 mb-0">Comments per page:</label>
                <select class="form-select form-select-sm w-auto" value="@_pageSize" @onchange="OnPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                <div class="ms-auto">
                    @* Pagination controls â€” only rendered when there is more than one page *@
                    @if (_commentsPage != null && _commentsPage.TotalPages > 1)
                    {
                        <nav aria-label="Comment pagination">
                            <ul class="pagination pagination-sm mb-0">
                                <li class="page-item @(_pageNumber == 1 ? "disabled" : "")">
                                    <button class="page-link" @onclick="PrevPage">Prev</button>
                                </li>
                                @{
                                    // Collapsed pagination: show first, last, and neighbors of current page with ellipses
                                    var totalPages = _commentsPage.TotalPages;
                                    var pagesToShow = new SortedSet<int>();
                                    pagesToShow.Add(1);
                                    pagesToShow.Add(totalPages);
                                    if (_pageNumber > 1) pagesToShow.Add(_pageNumber - 1);
                                    pagesToShow.Add(_pageNumber);
                                    if (_pageNumber < totalPages) pagesToShow.Add(_pageNumber + 1);

                                    int? lastShown = null;
                                    foreach (var pageNum in pagesToShow)
                                    {
                                        // Insert ellipsis between non-consecutive page numbers
                                        if (lastShown.HasValue && pageNum - lastShown.Value > 1)
                                        {
                                            <li class="page-item disabled"><span class="page-link">...</span></li>
                                        }
                                        <li class="page-item @(pageNum == _pageNumber ? "active" : "")">
                                            <button class="page-link" @onclick="() => GoToPage(pageNum)">@pageNum</button>
                                        </li>
                                        lastShown = pageNum;
                                    }
                                }
                                <li class="page-item @(_pageNumber == _commentsPage.TotalPages ? "disabled" : "")">
                                    <button class="page-link" @onclick="NextPage">Next</button>
                                </li>
                            </ul>
                        </nav>
                    }
                </div>
            </div>

            @if (_commentsPage?.Items == null || !_commentsPage.Items.Any())
            {
                <div class="no-comments">no comments yet.</div>
            }
            else
            {
                @foreach (var comment in _commentsPage.Items)
                {
                    <div class="comment">
                        <div class="comment-header">
                            <strong>@comment.AuthorUserName</strong>
                            <span class="comment-time">@comment.TimeCreated.ToString("MMM dd, yyyy")</span>
                            @* Show which comment this is a reply to, if applicable *@
                            @if (!string.IsNullOrEmpty(comment.ParentCommentAuthorUserName))
                            {
                                <span class="reply-info">replying to @comment.ParentCommentAuthorUserName</span>
                            }
                        </div>
                        @* Quote the parent comment content (truncated) when this is a reply *@
                        @if (!string.IsNullOrEmpty(comment.ParentCommentContent))
                        {
                            <div class="reply-citation">
                                <span class="reply-citation-author">@comment.ParentCommentAuthorUserName</span>
                                <span class="reply-citation-content">@TruncateContent(comment.ParentCommentContent, 200)</span>
                            </div>
                        }
                        <div class="comment-body">@comment.Content</div>

                        <div class="comment-footer">
                            @* ===== Vote buttons ===== *@
                            <AuthorizeView>
                                <Authorized>
                                    @* Active class is applied when the user has already voted in that direction *@
                                    <div class="vote-bar">
                                        <button class="vote-btn upvote @(GetUserVote(comment.CommentId) == 1 ? "active-up" : "")"
                                                @onclick="async () => await Vote(comment.CommentId, 1)"
                                                title="Upvote">â–²</button>
                                        @* Score colour changes based on positive/negative/zero value *@
                                        <span class="vote-score @(GetScore(comment.CommentId) > 0 ? "score-positive" : GetScore(comment.CommentId) < 0 ? "score-negative" : "score-zero")">
                                            @GetScore(comment.CommentId)
                                        </span>
                                        <button class="vote-btn downvote @(GetUserVote(comment.CommentId) == -1 ? "active-down" : "")"
                                                @onclick="async () => await Vote(comment.CommentId, -1)"
                                                title="Downvote">â–¼</button>
                                    </div>
                                </Authorized>
                                <NotAuthorized>
                                    @* Unauthenticated users see disabled vote buttons with a tooltip *@
                                    <div class="vote-bar vote-bar-locked" title="Log in to vote">
                                        <button class="vote-btn upvote locked" disabled>â–²</button>
                                        <span class="vote-score score-zero">@GetScore(comment.CommentId)</span>
                                        <button class="vote-btn downvote locked" disabled>â–¼</button>
                                    </div>
                                </NotAuthorized>
                            </AuthorizeView>

                            @* Reply button â€” only shown when not already replying to this comment *@
                            <AuthorizeView>
                                <Authorized Context="replyAuth">
                                    @if (_replyingToCommentId != comment.CommentId)
                                    {
                                        <button class="btn btn-link btn-sm p-0" @onclick="() => StartReply(comment.CommentId)">Reply</button>
                                    }
                                </Authorized>
                            </AuthorizeView>

                            @* Delete button â€” visible to admins and the comment's own author *@
                            @if (!comment.IsDeleted)
                            {
                                <AuthorizeView>
                                    <Authorized Context="deleteAuth">
                                        @if (deleteAuth.User.IsInRole("Admin") || deleteAuth.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == comment.AuthorId)
                                        {
                                            <button class="btn btn-link btn-sm text-danger p-0 ms-2" title="Delete comment"
                                                    @onclick="() => ConfirmAndDelete(comment.CommentId)" disabled="@_isSubmitting">
                                                Delete
                                            </button>
                                        }
                                    </Authorized>
                                </AuthorizeView>
                            }
                        </div>

                        @* Inline reply form â€” only shown for the comment currently being replied to *@
                        <AuthorizeView>
                            <Authorized Context="replyFormAuth">
                                @if (_replyingToCommentId == comment.CommentId)
                                {
                                    <div class="reply-form">
                                        <textarea class="form-control" rows="3" placeholder="Write a reply to @comment.AuthorUserName..." @bind="_replyContent"></textarea>
                                        <div class="mt-3">
                                            <button class="btn btn-primary btn-sm" @onclick="() => SubmitReply(comment.CommentId)" disabled="@_isSubmitting">Reply</button>
                                            <button class="btn btn-secondary btn-sm ms-1" @onclick="CancelReply">Cancel</button>
                                        </div>
                                    </div>
                                }
                            </Authorized>
                        </AuthorizeView>
                    </div>
                }
            }
        </div>

        <AuthorizeView>
            <Authorized>
                @* New top-level comment form at the bottom of the thread *@
                <div class="add-comment">
                    @if (!string.IsNullOrEmpty(_commentError))
                    {
                        <div class="alert alert-danger">@_commentError</div>
                    }
                    <textarea class="form-control" rows="3" placeholder="Write a comment..." @bind="_newCommentContent"></textarea>
                    <button class="btn btn-primary mt-3" @onclick="SubmitComment" disabled="@_isSubmitting">
                        @if (_isSubmitting)
                        {
                            <span class="spinner-border spinner-border-sm me-2"></span>
                        }
                        Post Comment
                    </button>
                </div>
            </Authorized>
            <NotAuthorized>
                @* Prompt unauthenticated users to log in or register to participate *@
                <div class="auth-prompt">
                    <span class="auth-prompt-icon">ðŸ’¬</span>
                    <p>Want to join the conversation?</p>
                    <div class="auth-prompt-links">
                        <a href="/login" class="auth-link">Log in</a>
                        <span>or</span>
                        <a href="/register" class="auth-link">Register</a>
                    </div>
                </div>
            </NotAuthorized>
        </AuthorizeView>
    </div>
}

@code {
    [Parameter]
    public int Id { get; set; } // Thread ID from the URL segment /thread/{Id}

    private ThreadDetailDto? _thread;
    private bool _isLoading = true;

    private PagedResult<CommentDto>? _commentsPage;
    private int _pageNumber = 1;
    private int _pageSize = 10;

    private string _newCommentContent = string.Empty;
    private string _commentError = string.Empty;
    private bool _isSubmitting;
    private int? _replyingToCommentId; // ID of the comment currently being replied to, or null
    private string _replyContent = string.Empty;

    // Local vote state â€” kept in sync after each vote API call to avoid full page reload
    private Dictionary<int, int> _voteScores = [];
    private Dictionary<int, int> _userVotes = [];

    protected override async Task OnParametersSetAsync()
    {
        // Re-runs when navigating between threads without a full component remount
        await LoadThread();
    }

    private async Task LoadThread()
    {
        _isLoading = true;
        try
        {
            _thread = await ThreadService.GetByIdAsync(Id);
            await LoadComments();
        }
        catch
        {
            // Treat any failure as thread not found
            _thread = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadComments(int? page = null)
    {
        var pageToLoad = page ?? _pageNumber;
        try
        {
            _commentsPage = await CommentService.GetByThreadAsync(Id, pageToLoad, _pageSize);
            if (_commentsPage != null)
            {
                _pageNumber = _commentsPage.PageNumber;
                PopulateVoteData(); // Sync local vote caches from the fresh server response
            }
        }
        catch
        {
            _commentsPage = null;
        }
        StateHasChanged();
    }

    // Seed local vote caches from the server response so votes render instantly without re-fetching
    private void PopulateVoteData()
    {
        if (_commentsPage?.Items == null) return;
        foreach (var c in _commentsPage.Items)
        {
            _voteScores[c.CommentId] = c.VoteScore;
            _userVotes[c.CommentId] = c.CurrentUserVote ?? 0;
        }
    }

    // Returns the net vote score for a comment, defaulting to 0 if not yet loaded
    private int GetScore(int commentId) =>
        _voteScores.TryGetValue(commentId, out var s) ? s : 0;

    // Returns the current user's vote for a comment (1, -1, or 0 if not voted)
    private int GetUserVote(int commentId) =>
        _userVotes.TryGetValue(commentId, out var v) ? v : 0;

    private async Task Vote(int commentId, int direction)
    {
        var result = await CommentService.CastVoteAsync(commentId, direction);
        if (result != null)
        {
            // Update local caches immediately so the UI reflects the new state without a full reload
            _voteScores[commentId] = result.NewScore;
            _userVotes[commentId] = result.UserVote;
        }
    }

    private async Task OnPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == _pageSize) return; // No-op if same size selected
        _pageSize = newSize;
        _pageNumber = 1; // Reset to first page on page size change
        await LoadComments(_pageNumber);
    }

    private void StartReply(int commentId)
    {
        // Enter reply mode for the selected comment
        _replyingToCommentId = commentId;
        _replyContent = string.Empty;
    }

    private void CancelReply()
    {
        // Exit reply mode without submitting
        _replyingToCommentId = null;
        _replyContent = string.Empty;
    }

    private async Task SubmitReply(int parentCommentId)
    {
        if (string.IsNullOrWhiteSpace(_replyContent)) return;
        _isSubmitting = true;
        try
        {
            var dto = new CreateCommentDto(_replyContent.Trim(), parentCommentId);
            var response = await CommentService.CreateAsync(Id, dto);
            if (response.IsSuccessStatusCode)
            {
                // Clear reply state and refresh comments on success
                _replyingToCommentId = null;
                _replyContent = string.Empty;
                await LoadComments(_pageNumber);
            }
        }
        catch { }
        finally { _isSubmitting = false; }
    }

    // Shortens long parent comment content for the reply citation block
    private static string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength) return content;
        return content[..maxLength] + "...";
    }

    private async Task SubmitComment()
    {
        _commentError = string.Empty;
        if (string.IsNullOrWhiteSpace(_newCommentContent))
        {
            _commentError = "Comment cannot be empty.";
            return;
        }
        _isSubmitting = true;
        try
        {
            var dto = new CreateCommentDto(_newCommentContent.Trim());
            var response = await CommentService.CreateAsync(Id, dto);
            if (response.IsSuccessStatusCode)
            {
                // Clear the input and refresh comments on success
                _newCommentContent = string.Empty;
                await LoadComments(_pageNumber);
            }
            else
            {
                _commentError = "Failed to post comment.";
            }
        }
        catch (Exception ex)
        {
            _commentError = ex.Message;
        }
        finally { _isSubmitting = false; }
    }

    private async Task PrevPage()
    {
        if (_commentsPage is null || !_commentsPage.HasPreviousPage) return;
        _pageNumber = Math.Max(1, _pageNumber - 1);
        await LoadComments(_pageNumber);
    }

    private async Task NextPage()
    {
        if (_commentsPage is null || !_commentsPage.HasNextPage) return;
        _pageNumber = Math.Min(_commentsPage.TotalPages, _pageNumber + 1);
        await LoadComments(_pageNumber);
    }

    private async Task GoToPage(int p)
    {
        if (_commentsPage is null) return;
        if (p < 1 || p > _commentsPage.TotalPages) return;
        _pageNumber = p;
        await LoadComments(_pageNumber);
    }

    private async Task ConfirmAndDelete(int commentId)
    {
        // Show a browser confirm dialog before deleting
        var confirmed = await JS.InvokeAsync<bool>("confirm", "Are you sure you want to delete this comment?");
        if (!confirmed) return;
        await DeleteComment(commentId);
    }

    private async Task DeleteComment(int commentId)
    {
        _commentError = string.Empty;
        _isSubmitting = true;
        try
        {
            var response = await CommentService.DeleteAsync(commentId);
            if (response.IsSuccessStatusCode)
            {
                // Refresh the current page after deletion
                await LoadComments(_pageNumber);
            }
            else
            {
                _commentError = "Failed to delete comment.";
            }
        }
        catch (Exception ex)
        {
            _commentError = ex.Message;
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private static string FormatDate(DateTime dt) => dt.ToString("MMM dd, yyyy");
}```
## File: \src\Forum.Blazor\Components\Pages\Threads.razor
```razor
@page "/threads"
@inject HttpClient Http

<link rel="stylesheet" href="/css/forum-table.css" />

<div class="forum-view-wrapper">
    @* Breadcrumb navigation â€” shows category name if filtered, otherwise "ALL" *@
    <nav class="breadcrumb-trail">
        <a href="/">Sport Forum</a>
        <span> &rsaquo; </span>
        <span class="breadcrumb-current">@(CategoryName?.ToUpper() ?? "ALL")</span>
    </nav>

    <div class="forum-intro">
        <h1>@(CategoryName ?? "All Threads")</h1>
        <p>the place for debates on current affairs and recent past of motorsport.</p>
    </div>

    <div class="forum-table-container">
        <table class="autosport-table">
            <thead>
                <tr>
                    <th class="th-main">TOPIC</th>
                    <th class="th-stats text-center">STATS</th>
                    <th class="th-last">DATE</th>
                </tr>
            </thead>
            <tbody>
                @* Loading / empty / content states *@
                @if (threads == null) 
                { 
                    <tr><td colspan="3" class="no-results">loading threads...</td></tr> 
                }
                else if (!threads.Items.Any())
                {
                    <tr><td colspan="3" class="no-results">no topics found.</td></tr>
                }
                else 
                {
                    @foreach (var t in threads.Items) 
                    {
                        <tr class="topic-row">
                            <td class="td-main">
                                <div class="topic-box">
                                    <div class="topic-info">
                                        <a href="/thread/@t.Id" class="topic-title">@t.Title</a>
                                        <div class="topic-subtext">started by <span class="author-bold">@t.Author</span></div>
                                    </div>
                                </div>
                            </td>
                            <td class="td-stats text-center">
                                <div class="stat-group">
                                    <span class="stat-count">@t.CommentsCount</span>
                                    <span class="stat-label">REPLIES</span>
                                </div>
                            </td>
                            <td class="td-last">
                                <div class="last-post-box">
                                    <span class="lp-time">@t.Created.ToString("MMM dd, yyyy")</span>
                                </div>
                            </td>
                        </tr>
                    }
                }
            </tbody>
        </table>
    </div>
</div>

@code {
    [Parameter] public string? CategoryName { get; set; } // Optional filter; null means show all threads
    private PagedResult<ThreadDto>? threads; // Null while loading

    protected override async Task OnParametersSetAsync() {
        await LoadThreads(); // Reload whenever the category filter changes
    }

    private async Task LoadThreads() {
        var url = "api/threads?pageSize=20";
        // Append category filter to the query string if provided
        if (!string.IsNullOrEmpty(CategoryName)) url += $"&CategoryName={Uri.EscapeDataString(CategoryName)}";
        try {
            var response = await Http.GetFromJsonAsync<PagedResult<ThreadSummaryDto>>(url);
            if (response != null)
            {
                // Map ThreadSummaryDto to the local ThreadDto with view-friendly property names
                threads = PagedResult<ThreadDto>.Create(
                    response.Items.Select(t => new ThreadDto(
                        t.ThreadId,
                        t.AuthorUserName,
                        t.TimeCreated,
                        t.Title,
                        "", // Body not needed in the listing view
                        t.CommentCount
                    )).ToList(),
                    response.TotalCount,
                    response.PageNumber,
                    response.PageSize
                );
            }
        } catch {
            // Fall back to an empty result on network or deserialization failure
            threads = PagedResult<ThreadDto>.Create(new List<ThreadDto>(), 0, 1, 20);
        }
    }

    // Local view model â€” maps API DTO fields to names used in the template above
    private record ThreadDto(
        int Id,
        string Author,
        DateTime Created,
        string Title,
        string Body,
        int CommentsCount
    );
}```
## File: \src\Forum.Blazor\Components\App.razor
```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
    <base href="/"/>
    <link rel="stylesheet" href="bootstrap/bootstrap.min.css"/>
    <link rel="stylesheet" href="app.css"/>
    <link rel="stylesheet" href="Forum.Blazor.styles.css"/>
    <link rel="icon" type="image/png" href="favicon.png"/>
    <HeadOutlet @rendermode="RenderMode.InteractiveServer"/>
</head>
<body>
<Routes @rendermode="RenderMode.InteractiveServer"/>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>
<script src="_framework/blazor.web.js"></script>
</body>
</html>

```
## File: \src\Forum.Blazor\Components\Routes.razor
```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)"/>
        <FocusOnNavigate RouteData="routeData" Selector="h1"/>
    </Found>
</Router>

```
## File: \src\Forum.Blazor\Components\_Imports.razor
```razor
@using System.Net.Http
@using System.Net.Http.Json
@using System.Linq
@using System.ComponentModel.DataAnnotations
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.JSInterop
@using Forum.Blazor
@using Forum.Blazor.Components
@using Forum.Blazor.Interfaces
@using Forum.Blazor.Services
@using Forum.Application.DTOs.Category
@using Forum.Application.DTOs.Thread
@using Forum.Application.DTOs.Comment
@using Forum.Application.DTOs.User
@using Forum.Application.Common.Models
```
## File: \src\Forum.Blazor\Interfaces\IAuthClientService.cs
```cs
using Forum.Blazor.Models;

namespace Forum.Blazor.Interfaces;

/// <summary>
/// Client-side authentication service for login, registration, and token management.
/// </summary>
public interface IAuthClientService
{
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
}```
## File: \src\Forum.Blazor\Interfaces\ITokenStorageService.cs
```cs
namespace Forum.Blazor.Interfaces;

/// <summary>
/// Persists and retrieves the JWT authentication token for the current session.
/// </summary>
public interface ITokenStorageService
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
}
```
## File: \src\Forum.Blazor\Models\AuthResult.cs
```cs
namespace Forum.Blazor.Models;

/// <summary>
/// Wraps the outcome of an authentication operation (login or register) with success/error details.
/// </summary>
public class AuthResult
{
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public LoginResponse? Data { get; set; }

    public static AuthResult Success(LoginResponse? data = null) =>
        new() { Succeeded = true, Data = data };

    public static AuthResult Failure(string error) =>
        new() { Succeeded = false, ErrorMessage = error };
}
```
## File: \src\Forum.Blazor\Models\LoginRequest.cs
```cs
namespace Forum.Blazor.Models;

/// <summary>
/// Model for user login form data, sent to POST /api/auth/login.
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
```
## File: \src\Forum.Blazor\Models\LoginResponse.cs
```cs
namespace Forum.Blazor.Models;

/// <summary>
/// Response returned by the API on successful login, containing the JWT token and user details.
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
}
```
## File: \src\Forum.Blazor\Models\RegisterRequest.cs
```cs
namespace Forum.Blazor.Models;

/// <summary>
/// Model for user registration form data. ConfirmPassword is used for client-side
/// validation only and is excluded when sending to POST /api/auth/register.
/// </summary>
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
```
## File: \src\Forum.Blazor\Services\ApiAuthenticationStateProvider.cs
```cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Forum.Blazor.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace Forum.Blazor.Services;

/// <summary>
/// AuthenticationStateProvider that determines the current user's identity
/// by reading and parsing the JWT token stored in session storage.
/// </summary>
public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorageService _tokenStorage;

    // A ClaimsPrincipal with no identity â€” represents an unauthenticated (anonymous) user.
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public ApiAuthenticationStateProvider(ITokenStorageService tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    /// <summary>
    /// Called by Blazor's auth system to determine the current user.
    /// Reads the JWT from session storage, parses its claims, and checks expiry.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return new AuthenticationState(_anonymous);

        var claims = ParseClaimsFromJwt(token);
        if (claims == null)
        {
            await _tokenStorage.RemoveTokenAsync();
            return new AuthenticationState(_anonymous);
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Immediately updates the authentication state after a successful login.
    /// Triggers re-evaluation of all AuthorizeView components without a page reload.
    /// </summary>
    public void MarkUserAsAuthenticated(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var identity = claims != null
            ? new ClaimsIdentity(claims, "jwt")
            : new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    /// <summary>
    /// Resets the authentication state to anonymous after logout.
    /// Triggers re-evaluation of all AuthorizeView components.
    /// </summary>
    public void MarkUserAsLoggedOut()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    /// <summary>
    /// Parses claims directly from the JWT payload.
    /// </summary>
    private IEnumerable<Claim>? ParseClaimsFromJwt(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Reject expired tokens â€” treat user as logged out
            if (jwtToken.ValidTo < DateTime.UtcNow)
                return null;

            var claims = new List<Claim>();
            foreach (var claim in jwtToken.Claims)
            {
                var mappedType = claim.Type switch
                {
                    "sub" => ClaimTypes.NameIdentifier,
                    "unique_name" => ClaimTypes.Name,
                    "email" => ClaimTypes.Email,
                    "role" => ClaimTypes.Role,
                    _ => claim.Type
                };
                claims.Add(new Claim(mappedType, claim.Value));
            }

            return claims;
        }
        catch
        {
            return null;
        }
    }
}
```
## File: \src\Forum.Blazor\Services\ApiClientBase.cs
```cs
using System.Net.Http.Headers;
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

    /// <summary>
    /// Initializes the base client with an HTTP client factory and token storage for authenticated API calls.
    /// </summary>
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
```
## File: \src\Forum.Blazor\Services\AuthService.cs
```cs
using System.Text.Json;
using Forum.Blazor.Interfaces;
using Forum.Blazor.Models;

namespace Forum.Blazor.Services;

/// <summary>
/// Handles authentication operations (login, register, logout) against the Forum.Api.
/// </summary>
public class AuthService : IAuthClientService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;
    private readonly ApiAuthenticationStateProvider _authStateProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthService(
        HttpClient httpClient,
        ITokenStorageService tokenStorage,
        ApiAuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _authStateProvider = authStateProvider;
    }

    /// <summary>
    /// Sends credentials to POST /api/auth/login.
    /// </summary>
    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { request.Username, request.Password });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(ApiClientBase.TryExtractError(errorBody) ?? "Invalid username or password.");
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            if (loginResponse is null || string.IsNullOrEmpty(loginResponse.Token))
                return AuthResult.Failure("Login failed: no token received.");

            await _tokenStorage.SetTokenAsync(loginResponse.Token);
            _authStateProvider.MarkUserAsAuthenticated(loginResponse.Token);

            return AuthResult.Success(loginResponse);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Login failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends registration data to POST /api/auth/register.
    /// </summary>
    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", new { request.Username, request.Email, request.Password });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return AuthResult.Failure(ApiClientBase.TryExtractError(errorBody) ?? "Registration failed.");
            }

            return AuthResult.Success();
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Registration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the stored JWT and resets the authentication state to anonymous.
    /// </summary>
    public async Task LogoutAsync()
    {
        await _tokenStorage.RemoveTokenAsync();
        _authStateProvider.MarkUserAsLoggedOut();
    }

    public Task<string?> GetTokenAsync() => _tokenStorage.GetTokenAsync();

}
```
## File: \src\Forum.Blazor\Services\CategoryService.cs
```cs
using Forum.Application.DTOs.Category;
using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// API service for category operations. Calls Forum.Api category endpoints.
/// </summary>
public class CategoryService : ApiClientBase
{
    public CategoryService(IHttpClientFactory httpClientFactory, ITokenStorageService tokenStorage)
        : base(httpClientFactory, tokenStorage)
    {
    }

    /// <summary>
    /// GET /api/categories â€” returns all categories with their thread counts.
    /// </summary>
    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await GetAsync<List<CategoryDto>>("api/categories") ?? [];
    }

    /// <summary>
    /// GET /api/categories/{id} â€” returns a single category by ID.
    /// </summary>
    public async Task<CategoryDto?> GetByIdAsync(int id)
    {
        return await GetAsync<CategoryDto>($"api/categories/{id}");
    }

    /// <summary>
    /// POST /api/categories â€” creates a new category. Requires admin role.
    /// </summary>
    public async Task<HttpResponseMessage> CreateAsync(CreateCategoryDto dto)
    {
        return await PostAsync("api/categories", dto);
    }

    /// <summary>
    /// PUT /api/categories/{id} â€” updates a category's name. Requires admin role.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(int id, UpdateCategoryDto dto)
    {
        return await PutAsync($"api/categories/{id}", dto);
    }

    /// <summary>
    /// DELETE /api/categories/{id} â€” deletes a category (hard delete). Requires admin role.
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(int id)
    {
        return await DeleteAsync($"api/categories/{id}");
    }
}
```
## File: \src\Forum.Blazor\Services\CommentService.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.DTOs.Vote;
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
        return await GetAuthenticatedAsync<PagedResult<CommentDto>>(
            $"api/comments/thread/{threadId}?pageNumber={pageNumber}&pageSize={pageSize}");
    }

    /// <summary>
    /// GET /api/comments?authorId=...&pageNumber=...&pageSize=...
    /// Returns paginated comments filtered by author.
    /// </summary>
    public async Task<PagedResult<CommentDto>?> GetByAuthorAsync(string authorId, int pageNumber = 1, int pageSize = 20, string? sortBy = null)
    {
        var url = $"api/comments?authorId={authorId}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(sortBy))
            url += $"&sortBy={sortBy}";
        return await GetAsync<PagedResult<CommentDto>>(url);
    }

    /// <summary>
    /// GET /api/comments/{id} â€” returns a single comment by ID.
    /// </summary>
    public async Task<CommentDto?> GetByIdAsync(int id)
    {
        return await GetAsync<CommentDto>($"api/comments/{id}");
    }

    /// <summary>
    /// POST /api/comments/{threadId} â€” creates a new comment in the given thread.
    /// </summary>
    public async Task<HttpResponseMessage> CreateAsync(int threadId, CreateCommentDto dto)
    {
        return await PostAsync($"api/comments/{threadId}", dto);
    }

    /// <summary>
    /// PUT /api/comments/{id} â€” updates a comment's content.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(int id, UpdateCommentDto dto)
    {
        return await PutAsync($"api/comments/{id}", dto);
    }

    /// <summary>
    /// DELETE /api/comments/{id} â€” soft-deletes a comment (content becomes "[deleted]").
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(int id)
    {
        return await base.DeleteAsync($"api/comments/{id}");
    }

    /// <summary>
    /// POST /api/comments/{commentId}/votes â€” casts or toggles a vote (1 or -1).
    /// Returns updated score and user vote state. Returns null on failure.
    /// </summary>
    public async Task<VoteResponseDto?> CastVoteAsync(int commentId, int value)
    {
        var response = await PostAsync($"api/comments/{commentId}/votes", new CastVoteDto(value));
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<VoteResponseDto>(JsonOptions);
        return null;
    }
}```
## File: \src\Forum.Blazor\Services\ThreadService.cs
```cs
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
    /// GET /api/threads/{id} â€” returns full thread details including body comment and all comments.
    /// </summary>
    public async Task<ThreadDetailDto?> GetByIdAsync(int id)
    {
        return await GetAsync<ThreadDetailDto>($"api/threads/{id}");
    }

    /// <summary>
    /// POST /api/threads â€” creates a new thread with a body (stored as the first comment).
    /// </summary>
    public async Task<HttpResponseMessage> CreateAsync(CreateThreadDto dto)
    {
        return await PostAsync("api/threads", dto);
    }

    /// <summary>
    /// PUT /api/threads/{id} â€” updates a thread's title or category.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(int id, UpdateThreadDto dto)
    {
        return await PutAsync($"api/threads/{id}", dto);
    }

    /// <summary>
    /// DELETE /api/threads/{id} â€” deletes a thread (hard delete).
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
```
## File: \src\Forum.Blazor\Services\TokenStorageService.cs
```cs
using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// Stores the JWT token in memory for the current circuit/session.
/// </summary>
public class TokenStorageService : ITokenStorageService
{
    private string? _token;

    /// <summary>
    /// Retrieves the stored JWT token, or null if none exists.
    /// </summary>
    public Task<string?> GetTokenAsync() => Task.FromResult(_token);

    /// <summary>
    /// Stores the JWT token. Called after successful login/register.
    /// </summary>
    public Task SetTokenAsync(string token)
    {
        _token = token;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the JWT token. Called on logout.
    /// </summary>
    public Task RemoveTokenAsync()
    {
        _token = null;
        return Task.CompletedTask;
    }
}
```
## File: \src\Forum.Blazor\Services\UserService.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.User;
using Forum.Blazor.Interfaces;

namespace Forum.Blazor.Services;

/// <summary>
/// API service for user profile operations. Calls Forum.Api user endpoints.
/// </summary>
public class UserService : ApiClientBase
{
    public UserService(IHttpClientFactory httpClientFactory, ITokenStorageService tokenStorage)
        : base(httpClientFactory, tokenStorage)
    {
    }

    /// <summary>
    /// GET /api/users?pageNumber=...&pageSize=...&sortBy=... â€” returns paginated users (admin only).
    /// </summary>
    public async Task<PagedResult<UserDto>?> GetPagedAsync(int pageNumber = 1, int pageSize = 10, string? sortBy = null)
    {
        var url = $"api/users?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(sortBy))
            url += $"&sortBy={sortBy}";
        return await GetAuthenticatedAsync<PagedResult<UserDto>>(url);
    }

    /// <summary>
    /// GET /api/users/{id} â€” returns user profile with recent threads and comments.
    /// </summary>
    public async Task<UserProfileDto?> GetProfileAsync(string id)
    {
        return await GetAsync<UserProfileDto>($"api/users/{id}");
    }

    /// <summary>
    /// PUT /api/users/{id} â€” updates user profile (username, email).
    /// Supports partial updates â€” only provided fields are changed.
    /// </summary>
    public async Task<HttpResponseMessage> UpdateAsync(string id, UpdateUserProfileDto dto)
    {
        return await PutAsync($"api/users/{id}", dto);
    }

    /// <summary>
    /// POST /api/users/{id}/change-password â€” changes the user's password.
    /// </summary>
    public async Task<HttpResponseMessage> ChangePasswordAsync(string id, string currentPassword, string newPassword)
    {
        return await PostAsync($"api/users/{id}/change-password", new { CurrentPassword = currentPassword, NewPassword = newPassword });
    }

    /// <summary>
    /// DELETE /api/users/{id} â€” soft-deletes the user account.
    /// The user's display name becomes "Deleted User" across all threads and comments.
    /// </summary>
    public new async Task<HttpResponseMessage> DeleteAsync(string id)
    {
        return await base.DeleteAsync($"api/users/{id}");
    }
}
```
## File: \src\Forum.Blazor\wwwroot\css\auth.css
```css
/* Auth Pages (Login & Register) */

.auth-container {
    max-width: 420px;
    margin: 0 auto;
    padding: 40px 20px;
}

.auth-card {
    background: var(--bg-card);
    border-radius: 12px;
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.25);
    border: 1px solid var(--border-subtle);
    overflow: hidden;
}

.auth-card-header {
    padding: 24px 30px 0;
    text-align: center;
}

.auth-card-header h3 {
    font-family: 'DM Sans', sans-serif;
    color: var(--text-primary);
    font-size: 1.6em;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 2px;
    margin: 0;
}

.auth-card-body {
    padding: 30px;
}

.auth-card-body .form-label {
    display: block;
    margin-bottom: 8px;
    font-weight: 500;
    color: var(--text-primary);
    font-size: 14px;
    text-transform: uppercase;
    letter-spacing: 1px;
}

.auth-card-body .form-control {
    border-radius: 8px;
}

.auth-card-body .btn-primary {
    width: 100%;
}

.auth-card-body .alert-danger {
    background: rgba(196, 64, 64, 0.2);
    color: #e0a0a0;
    border: 1px solid rgba(196, 64, 64, 0.3);
    border-radius: 8px;
}

.auth-card-body .alert-success {
    background: rgba(64, 156, 64, 0.2);
    color: #a0e0a0;
    border: 1px solid rgba(64, 156, 64, 0.3);
    border-radius: 8px;
}

.auth-link {
    display: block;
    text-align: center;
    margin-top: 16px;
    color: var(--accent-blue);
    text-decoration: none;
    font-size: 14px;
}

.auth-link:hover {
    color: var(--accent-blue-hover);
    text-decoration: underline;
}
```
## File: \src\Forum.Blazor\wwwroot\css\category.css
```css
/* Category Page Styles */

.category-page {
    max-width: 960px;
    margin: 0 auto;
    padding: 16px 20px;
}

.category-hero {
    position: relative;
    height: 180px;
    border-radius: 10px;
    overflow: hidden;
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 16px;
    box-shadow: 0 10px 24px rgba(0, 0, 0, 0.3);
    width: 100%;
    background-color: #1a1f26;
}

/* Sport icon decoration */
.category-hero::after {
    position: absolute;
    right: 60px;
    top: 50%;
    transform: translateY(-50%);
    font-size: 110px;
    opacity: 0.7;
    z-index: 0;
    line-height: 1;
}

/* Category-specific accent gradients and icons */
.category-hero.football {
    background: linear-gradient(135deg, #2e4238 0%, #345040 50%, #3a5a48 100%);
}
.category-hero.football::after {
    content: 'âš½';
}

.category-hero.basketball {
    background: linear-gradient(135deg, #3a3428 0%, #4a3c24 50%, #5a4228 100%);
}
.category-hero.basketball::after {
    content: 'ðŸ€';
}

.category-hero.tennis {
    background: linear-gradient(135deg, #36382a 0%, #424028 50%, #4e4a26 100%);
}
.category-hero.tennis::after {
    content: 'ðŸŽ¾';
}

.category-hero.formula1 {
    background: linear-gradient(135deg, #3a2e30 0%, #4a3234 50%, #583838 100%);
}
.category-hero.formula1::after {
    content: 'ðŸŽï¸';
}

.category-hero.hockey {
    background: linear-gradient(135deg, #2e3640 0%, #323c4e 50%, #38465e 100%);
}
.category-hero.hockey::after {
    content: 'ðŸ’';
}

.category-hero.golf {
    background: linear-gradient(135deg, #2e3a32 0%, #344840 50%, #3a5445 100%);
}
.category-hero.golf::after {
    content: 'â›³';
}

.category-hero.cycling {
    background: linear-gradient(135deg, #322e3a 0%, #3c3648 50%, #483e5c 100%);
}
.category-hero.cycling::after {
    content: 'ðŸš´';
}

.category-hero.rugby {
    background: linear-gradient(135deg, #38322a 0%, #443a28 50%, #504030 100%);
}
.category-hero.rugby::after {
    content: 'ðŸ‰';
}

.hero-content {
    position: relative;
    z-index: 2;
    text-align: center;
    color: #d1d5db;
    padding: 20px 40px;
    background: rgba(28, 32, 38, 0.9);
    backdrop-filter: blur(10px);
    border-radius: 10px;
    border: 1px solid rgba(255, 255, 255, 0.08);
    max-width: 600px;
    animation: fadeInUp 1s ease-out;
    box-shadow: 0 6px 20px rgba(0, 0, 0, 0.3);
}

@keyframes fadeInUp {
    from {
        opacity: 0;
        transform: translateY(30px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

.category-title {
    font-size: 1.5em;
    font-weight: 800;
    margin-bottom: 0;
    text-transform: uppercase;
    letter-spacing: 3px;
    color: #d1d5db;
    text-shadow: 0 2px 10px rgba(0, 0, 0, 0.5);
    line-height: 1.1;
}

.category-description {
    font-size: 1em;
    opacity: 0.85;
    line-height: 1.5;
    font-weight: 300;
    color: #8b949e;
}

.category-page .forum-view-wrapper {
    max-width: none;
    background: transparent;
    border-radius: 0;
    overflow: visible;
    box-shadow: none;
    border: none;
    margin-bottom: 0;
    padding: 8px 0;
    width: 100%;
}

.autosport-table {
    width: 100%;
}

.threads-table {
    width: 100%;
    border-collapse: collapse;
}

.threads-table th {
    background: #252a31;
    color: #8b949e;
    padding: 12px 15px;
    text-align: left;
    font-weight: 600;
    text-transform: uppercase;
    font-size: 11px;
    letter-spacing: 1px;
    border-bottom: none;
}

.threads-table td {
    padding: 14px 15px;
    border-bottom: none;
}

.thread-row:hover {
    background: rgba(110, 158, 207, 0.05);
}

.thread-title-cell {
    width: 50%;
}

.thread-title {
    color: #6e9ecf;
    text-decoration: none;
    font-weight: 600;
    font-size: 15px;
}

.thread-title:hover {
    color: #85b1db;
    text-decoration: underline;
}

.author-cell {
    width: 20%;
    color: #8b949e;
    font-weight: 500;
}

.replies-cell {
    width: 15%;
    color: #6e9ecf;
    text-align: center;
    font-weight: 600;
    padding: 4px 8px;
    border-radius: 4px;
}

.created-cell,
.activity-cell {
    color: #8b949e;
    font-size: 13px;
    white-space: nowrap;
}

.created-cell .date-line,
.activity-cell .date-line {
    display: block;
    font-weight: 600;
}

.created-cell .time-line,
.activity-cell .time-line {
    display: block;
    font-size: 11px;
    color: #6b7280;
}

/* Make dropdown arrows visible on dark background */
.form-select {
    background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3e%3cpath fill='none' stroke='%23ffffff' stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='m2 5 6 6 6-6'/%3e%3c/svg%3e");
}

.loading, .no-threads {
    text-align: center;
    padding: 60px 20px;
    color: #8b949e;
    font-style: italic;
    background: #252a31;
    border-radius: 12px;
    border: 1px solid rgba(255, 255, 255, 0.06);
    margin: 20px 0;
}

.create-thread-section {
    margin-top: 12px;
    text-align: center;
}

.auth-prompt {
    text-align: center;
    padding: 24px 20px;
    margin-top: 30px;
    background: #252a31;
    border-radius: 8px;
    border: 1px solid rgba(255, 255, 255, 0.06);
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.auth-prompt-icon {
    font-size: 1.5em;
    display: block;
    margin-bottom: 8px;
}

.auth-prompt p {
    color: #8b949e;
    margin: 0 0 12px 0;
    font-size: 15px;
}

.auth-prompt-links {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    color: #8b949e;
    font-size: 14px;
}

.auth-prompt-links .auth-link {
    color: #6e9ecf;
    text-decoration: none;
    font-weight: 600;
}

.auth-prompt-links .auth-link:hover {
    color: #85b1db;
    text-decoration: underline;
}
```
## File: \src\Forum.Blazor\wwwroot\css\create-thread.css
```css
/* Create Thread Page Styles */

.create-thread-container {
    max-width: 960px;
    margin: 0 auto;
    padding: 28px 20px;
    display: flex;
    flex-direction: column;
}

.form-header {
    text-align: center;
    margin-bottom: 30px;
    padding: 30px;
    background: #252a31;
    border-radius: 12px;
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.25);
    border: 1px solid rgba(255, 255, 255, 0.06);
}

.form-header h1 {
    margin-bottom: 10px;
    color: #d1d5db;
    font-size: 1.6em;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 2px;
}

.form-header p {
    margin: 0;
    color: #8b949e;
    font-size: 1.1em;
}

.form-group {
    margin-bottom: 20px;
}

.form-group label {
    display: block;
    margin-bottom: 8px;
    font-weight: 500;
    color: #d1d5db;
    font-size: 14px;
    text-transform: uppercase;
    letter-spacing: 1px;
}

.create-thread-container .form-control {
    width: 100%;
    padding: 12px 16px;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 8px;
    font-size: 14px;
    font-family: inherit;
    background: rgba(255, 255, 255, 0.04);
    color: #d1d5db;
    transition: border-color 0.3s ease, box-shadow 0.3s ease, background 0.3s ease, color 0.3s ease;
    box-sizing: border-box;
}

.create-thread-container .form-control:focus {
    outline: none;
    border-color: #6e9ecf;
    box-shadow: 0 0 0 2px rgba(110, 158, 207, 0.2);
    background: rgba(255, 255, 255, 0.07);
    color: #ffffff;
}

.form-actions {
    display: flex;
    gap: 12px;
    margin-top: 30px;
}


.submitting, .error-message {
    text-align: center;
    padding: 20px;
    margin: 20px 0;
    border-radius: 8px;
    font-size: 14px;
}

.submitting {
    background: rgba(74, 114, 153, 0.2);
    color: #85b1db;
    border: 1px solid rgba(74, 114, 153, 0.3);
    animation: pulse 1.5s infinite;
}

@keyframes pulse {
    0% {
        opacity: 1;
    }
    50% {
        opacity: 0.5;
    }
    100% {
        opacity: 1;
    }
}

.error-message {
    background: rgba(196, 64, 64, 0.2);
    color: #e0a0a0;
    border: 1px solid rgba(196, 64, 64, 0.3);
}

.category-display:disabled {
    color: white;
    -webkit-text-fill-color: white;
    opacity: 1;
}
```
## File: \src\Forum.Blazor\wwwroot\css\forum-table.css
```css
/*  Threads page  */
.forum-view-wrapper { max-width: 960px; margin: 0 auto; padding: 28px 20px; }
.breadcrumb-trail {
    font-size: 12px;
    font-weight: 700;
    color: #8b949e;
    margin-bottom: 16px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}
.forum-intro h1 {
    font-size: 1.6em;
    font-weight: 800;
    color: #d1d5db;
    margin: 0;
    letter-spacing: 0.5px;
}
.forum-intro p {
    font-size: 14px;
    color: #8b949e;
    margin: 10px 0 30px 0;
    line-height: 1.5;
    max-width: 850px;
}
.forum-toolbar {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    border-bottom: 3px solid rgba(255, 255, 255, 0.1);
}
.category-tabs-row { display: flex; gap: 2px; }
.tab-link {
    border: none;
    background: #2d333b;
    padding: 12px 22px;
    font-weight: 900;
    font-size: 12px;
    cursor: pointer;
    border-radius: 3px 3px 0 0;
    color: #8b949e;
    transition: 0.2s;
}
.tab-link:hover { background: #353c45; }
.tab-link.active { background: #c44040; color: #fff; }

/* Page search (dark) */
.forum-toolbar .forum-search-container { position: relative; margin-bottom: 8px; }
.forum-toolbar .search-input {
    border: 1px solid rgba(255, 255, 255, 0.1);
    background: #2d333b;
    color: #d1d5db;
    padding: 8px 10px 8px 30px;
    font-size: 13px;
    width: 250px;
    outline: none;
    border-radius: 4px;
}
.forum-toolbar .search-input::placeholder { color: #8b949e; }
.forum-toolbar .search-icon {
    position: absolute;
    left: 10px;
    top: 50%;
    transform: translateY(-50%);
    color: #8b949e;
    font-size: 12px;
}

.forum-table-container { background: #252a31; border: none; border-radius: 0 0 8px 8px; }
.autosport-table { width: 100%; border-collapse: collapse; }
.autosport-table th {
    background: #2d333b;
    padding: 12px 15px;
    font-size: 11px;
    font-weight: 800;
    color: #8b949e;
    border-bottom: none;
    text-align: left;
}
.topic-row { border-bottom: 1px solid rgba(255, 255, 255, 0.04); transition: 0.1s; }
.topic-row:hover { background: rgba(110, 158, 207, 0.04); }
.topic-row td { padding: 14px 15px; vertical-align: middle; }
.topic-box { display: flex; align-items: flex-start; gap: 15px; }
.status-indicator { color: #555; font-size: 18px; margin-top: 2px; }
.topic-title {
    color: #6e9ecf;
    font-weight: 700;
    text-decoration: none;
    font-size: 15px;
    line-height: 1.3;
    display: block;
}
.topic-title:hover { text-decoration: underline; color: #85b1db; }
.topic-subtext { font-size: 12px; color: #8b949e; margin-top: 5px; }
.author-bold { font-weight: 700; color: #8b949e; }
.td-stats { width: 120px; border-left: 1px solid rgba(255, 255, 255, 0.05); border-right: 1px solid rgba(255, 255, 255, 0.05); }
.stat-group { display: flex; flex-direction: column; }
.stat-count { font-weight: 800; font-size: 14px; color: #d1d5db; }
.stat-label { font-size: 10px; color: #666; font-weight: 800; }
.text-center { text-align: center; }
.td-last { width: 220px; padding-left: 20px !important; }
.last-post-box { display: flex; flex-direction: column; font-size: 13px; line-height: 1.5; }
.lp-time { font-weight: 700; color: #8b949e; }
.red-name { color: #c44040; font-weight: 800; }
.no-results { padding: 60px !important; text-align: center; color: #8b949e; font-style: italic; }
```
## File: \src\Forum.Blazor\wwwroot\css\home.css
```css
/* Home Page Styles */

/* ===== BACKGROUND ===== */
.home-wrapper {
    width: 100%;
    flex: 1;
    background: linear-gradient(168deg, #1c2026 0%, #15181d 40%, #1a1e24 70%, #1c2026 100%);
    position: relative;
}

.home-wrapper::before {
    content: '';
    position: absolute;
    inset: 0;
    background-image: radial-gradient(circle at 20% 50%, rgba(196, 64, 64, 0.03) 0%, transparent 50%),
                       radial-gradient(circle at 80% 20%, rgba(90, 138, 184, 0.03) 0%, transparent 50%);
    pointer-events: none;
    z-index: 0;
}

.home-overlay {
    width: 100%;
    background: transparent;
    padding: 28px 20px 24px;
    position: relative;
    z-index: 1;
}

/* ===== CONTENT LAYOUT ===== */
.home-content {
    max-width: 960px;
    margin: 0 auto;
}

.popular-section {
    margin-bottom: 28px;
}

.categories-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 10px;
}

@media (max-width: 768px) {
    .categories-grid {
        grid-template-columns: 1fr;
    }
}

/* ===== CATEGORIES ===== */
.categories-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.category-card {
    background: rgba(26, 30, 36, 0.95);
    border-radius: 6px;
    overflow: hidden;
    border: 1px solid rgba(255,255,255,0.07);
    transition: border-color 0.2s;
}

.category-card:hover {
    border-color: rgba(196, 64, 64, 0.4);
}

.category-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 10px 14px;
    background: rgba(33, 37, 48, 0.98);
    border-left: 4px solid #c44040;
    border-bottom: 1px solid rgba(196, 64, 64, 0.12);
}

.cat-name {
    color: #e2e6eb;
    font-size: 16px;
    font-weight: 800;
    text-transform: uppercase;
    letter-spacing: 2.5px;
    font-family: 'DM Sans', sans-serif;
    text-decoration: none;
    transition: color 0.15s;
}

.cat-name:hover {
    color: #85b1db;
    text-decoration: none;
}

.view-all-link {
    color: #8b949e;
    text-decoration: none;
    font-size: 13px;
    font-weight: 600;
    transition: color 0.15s;
}

.view-all-link:hover {
    color: #85b1db;
    text-decoration: none;
}

/* ===== THREAD ITEMS ===== */
.recent-threads { padding: 0; }

.thread-item {
    padding: 6px 14px 6px 18px;
    border-bottom: 1px solid rgba(255,255,255,0.04);
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 16px;
    transition: background 0.1s;
}

.thread-item:last-child { border-bottom: none; }
.thread-item:hover { background: rgba(110, 158, 207, 0.06); }

.thread-left {
    flex: 1;
    min-width: 0;
}

.thread-title {
    color: #b8cfe0;
    text-decoration: none;
    font-weight: 600;
    font-size: 15px;
    display: block;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    font-family: 'DM Sans', sans-serif;
    transition: color 0.15s;
}

.thread-title:hover {
    color: #d4e5f2;
    text-decoration: none;
}

.thread-meta {
    display: flex;
    gap: 5px;
    align-items: center;
    margin-top: 2px;
}

.author {
    font-weight: 700;
    font-size: 13px;
    color: #5a8ab8;
}

.dot { color: #2e3440; font-size: 13px; }
.date { font-size: 13px; color: #424957; }

.thread-right {
    display: flex;
    flex-direction: column;
    align-items: center;
    flex-shrink: 0;
    min-width: 36px;
}

.reply-count {
    font-size: 16px;
    font-weight: 800;
    color: #8b949e;
    line-height: 1;
    font-family: 'DM Sans', sans-serif;
}

.reply-label {
    font-size: 11px;
    color: #424957;
    text-transform: uppercase;
    letter-spacing: 0.8px;
    font-weight: 700;
}

/* ===== CATEGORY HEADER EXTRAS ===== */
.cat-count {
    font-size: 13px;
    color: #555d6b;
    font-weight: 600;
    font-family: 'DM Sans', sans-serif;
    margin-right: auto;
    margin-left: 12px;
}

/* ===== SECTION HEADERS & POPULAR THREADS ===== */
.section-title {
    font-family: 'DM Sans', sans-serif;
    font-size: 14px;
    font-weight: 800;
    text-transform: uppercase;
    letter-spacing: 2px;
    color: #8b949e;
    padding: 0 0 8px 0;
    border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    margin-bottom: 8px;
}

.popular-list {
    display: flex;
    flex-direction: column;
    gap: 4px;
}

.popular-item {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 12px;
    background: rgba(26, 30, 36, 0.7);
    border: 1px solid rgba(255, 255, 255, 0.04);
    border-radius: 4px;
    text-decoration: none;
    transition: border-color 0.2s;
}

.popular-item:hover {
    border-color: rgba(196, 64, 64, 0.3);
    text-decoration: none;
}

.popular-title {
    flex: 1;
    font-size: 15px;
    font-weight: 600;
    color: #b8cfe0;
    font-family: 'DM Sans', sans-serif;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.popular-meta {
    display: flex;
    gap: 5px;
    align-items: center;
    font-size: 13px;
}

.popular-category {
    color: #555d6b;
    font-size: 12px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.popular-replies {
    font-size: 14px;
    color: #8b949e;
    font-weight: 700;
    font-family: 'DM Sans', sans-serif;
    white-space: nowrap;
}

/* ===== LOADING / EMPTY ===== */
.loading {
    text-align: center;
    padding: 40px;
    color: #424957;
    font-size: 13px;
}

.no-threads {
    padding: 8px 18px;
    color: #555d6b;
    font-size: 12px;
    font-style: italic;
}```
## File: \src\Forum.Blazor\wwwroot\css\panel.css
```css
/* ===== PANEL STYLES ===== */

/* ===== Layout Containers ===== */
.panel-wrapper {
    padding: 28px 20px;
    width: 960px;
    max-width: 100%;
    margin: 0 auto;
    box-sizing: border-box;
}

.panel-title {
    font-size: 1.6em;
    font-weight: 800;
    color: #d1d5db;
    margin-bottom: 20px;
    letter-spacing: 0.5px;
}

.breadcrumb-trail {
    font-size: 12px;
    color: #8b949e;
    margin-bottom: 12px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

/* ===== Tabs ===== */
.panel-tabs {
    display: flex;
    gap: 0;
    margin-bottom: 28px;
    border-bottom: 2px solid rgba(255, 255, 255, 0.08);
}

.panel-tab {
    flex: 1;
    padding: 10px 12px;
    background: none;
    border: none;
    font-weight: 700;
    font-size: 14px;
    color: #8b949e;
    cursor: pointer;
    border-bottom: 2px solid transparent;
    margin-bottom: -2px;
    transition: color 0.15s, border-color 0.15s;
    text-align: center;
    white-space: nowrap;
}

.panel-tab:hover { color: #d1d5db; }
.panel-tab.active { color: #c44040; border-bottom-color: #c44040; }

/* ===== Narrow section variant ===== */
.panel-section-narrow {
    max-width: 420px;
    min-width: 0;
    margin-left: auto;
    margin-right: auto;
}

/* ===== Section Headers ===== */
.panel-section h2 {
    font-size: 16px;
    font-weight: 700;
    margin-bottom: 20px;
    color: #d1d5db;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

/* ===== Add Form ===== */
.panel-add-form {
    display: flex;
    gap: 12px;
    margin-bottom: 28px;
    align-items: center;
}

.panel-input {
    flex: 1;
    padding: 9px 14px;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 6px;
    font-size: 14px;
    outline: none;
    transition: border-color 0.15s;
    background: #2d333b;
    color: #d1d5db;
}

.panel-input::placeholder { color: #8b949e; }
.panel-input:focus { border-color: #c44040; }
.panel-input.full-width { width: 100%; }

.panel-input-inline {
    padding: 4px 8px;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 4px;
    font-size: 14px;
    width: 160px;
    background: #2d333b;
    color: #d1d5db;
}

/* ===== Table Styling ===== */
.panel-table {
    width: 100%;
    border-collapse: collapse;
    background: #1e2329;
    border-radius: 8px;
    overflow: hidden;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
    border: 1px solid rgba(255, 255, 255, 0.06);
}

.panel-table thead {
    background: #2d333b;
    color: #8b949e;
}

.panel-table th {
    padding: 13px 18px;
    text-align: center;
    font-size: 11px;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.8px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

/* Left-align name column for readability */
.panel-table th:nth-child(2),
.panel-table td:nth-child(2) {
    text-align: left;
}

.panel-table td {
    padding: 13px 18px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    font-size: 14px;
    color: #d1d5db;
    text-align: center;
    vertical-align: middle;
}

.panel-table tbody tr:last-child td { border-bottom: none; }
.panel-table tbody tr:hover { background: rgba(255, 255, 255, 0.03); }

/* ===== Action Cell ===== */
.panel-actions {
    display: flex;
    gap: 6px;
    justify-content: center;
    align-items: center;
}

/* ===== Button States ===== */
.btn-red {
    padding: 9px 20px;
    background: #c44040;
    color: #fff;
    border: none;
    border-radius: 6px;
    font-size: 14px;
    font-weight: 700;
    cursor: pointer;
    transition: background 0.15s;
    white-space: nowrap;
}

.btn-red:hover { background: #a83535; }
.btn-red:disabled { background: #555e68; color: #8b949e; cursor: not-allowed; opacity: 0.65; }

.btn-danger-sm {
    padding: 5px 14px;
    background: #a83535;
    color: #fff;
    border: none;
    border-radius: 4px;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.15s;
}

.btn-danger-sm:hover { background: #8f2c2c; }

/* Disabled â€” category has threads */
.btn-danger-sm:disabled {
    background: #3a3f47;
    color: #555e68;
    cursor: not-allowed;
    opacity: 1;
    pointer-events: auto;
    border: 1px solid rgba(255, 255, 255, 0.06);
}

.btn-danger-sm:disabled:hover { background: #3a3f47; }

.btn-success-sm {
    padding: 5px 14px;
    background: #3d8b5e;
    color: #fff;
    border: none;
    border-radius: 4px;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.15s;
}

.btn-success-sm:hover { background: #337a50; }

.btn-outline-sm {
    padding: 5px 14px;
    background: transparent;
    color: #d1d5db;
    border: 1px solid rgba(255, 255, 255, 0.12);
    border-radius: 4px;
    font-size: 12px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.15s;
}

.btn-outline-sm:hover { background: rgba(255, 255, 255, 0.05); }
.btn-outline-sm:disabled { color: #555e68; cursor: not-allowed; }

/* ===== Page Size Label ===== */
.panel-page-size-label {
    color: #8b949e;
}

/* ===== Pagination ===== */
.panel-pagination {
    display: flex;
    align-items: center;
    gap: 12px;
    justify-content: center;
    margin-top: 20px;
    font-size: 14px;
    color: #8b949e;
}

/* ===== Status Messages ===== */
.panel-status {
    padding: 11px 16px;
    border-radius: 6px;
    font-size: 14px;
    margin-bottom: 20px;
}

.panel-status.success {
    background: rgba(61, 139, 94, 0.15);
    color: #6ec99a;
    border: 1px solid rgba(61, 139, 94, 0.25);
}

.panel-status.error {
    background: rgba(168, 53, 53, 0.15);
    color: #e0a0a0;
    border: 1px solid rgba(168, 53, 53, 0.25);
}

/* ===== Responsive Table Wrapper ===== */
.panel-table-wrap {
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
}

/* ===== Comment Content ===== */
.comment-preview {
    max-width: 320px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    display: block;
}

/* ===== Responsive ===== */
@media (max-width: 768px) {
    .panel-wrapper {
        padding: 24px 12px;
    }

    .panel-tab {
        font-size: 13px;
        padding: 10px 8px;
    }

    .panel-table th,
    .panel-table td {
        padding: 10px 10px;
        font-size: 13px;
    }

    .comment-preview {
        max-width: 180px;
    }
}

@media (max-width: 480px) {
    .panel-tab {
        font-size: 12px;
        padding: 8px 6px;
    }

    .panel-table th,
    .panel-table td {
        padding: 8px 8px;
        font-size: 12px;
    }

    .comment-preview {
        max-width: 120px;
    }
}
```
## File: \src\Forum.Blazor\wwwroot\css\thread-details.css
```css
/* Thread Details Page Styles */

.thread-details {
    max-width: 960px;
    margin: 0 auto;
    padding: 28px 20px;
}

.breadcrumb,
.breadcrumb-trail {
    font-size: 12px;
    color: #8b949e;
    margin-bottom: 16px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.thread-header {
    margin-bottom: 30px;
    padding: 20px;
    background: #252a31;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
    border: 1px solid rgba(255, 255, 255, 0.06);
}

.thread-header h1 {
    margin-bottom: 12px;
    color: #d1d5db;
    font-size: 1.6em;
}

.thread-meta {
    display: flex;
    gap: 20px;
    color: #8b949e;
    font-size: 14px;
}

.thread-meta span {
    display: flex;
    align-items: center;
}

.thread-meta strong {
    color: #d1d5db;
    margin-left: 4px;
}

.body-comment {
    margin-bottom: 30px;
    padding: 20px;
    background: #2d333b;
    border-radius: 8px;
    border-left: 4px solid #6e9ecf;
}

.comment-content {
    line-height: 1.6;
    color: #d1d5db;
}

.comments-section {
    background: #252a31;
    border-radius: 8px;
    padding: 20px;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
    border: 1px solid rgba(255, 255, 255, 0.06);
}

.comments-section h3 {
    margin-bottom: 20px;
    color: #d1d5db;
    font-size: 1.3em;
    text-transform: uppercase;
}

.comment {
    margin-bottom: 20px;
    padding: 15px;
    background: #2d333b;
    border-radius: 8px;
    border-bottom: 1px solid rgba(255, 255, 255, 0.04);
}

.comment:last-child {
    border-bottom: none;
}

.comment-header {
    display: flex;
    gap: 15px;
    align-items: center;
    margin-bottom: 10px;
    font-size: 14px;
}

.comment-header strong {
    color: #d1d5db;
    font-weight: 600;
}

.comment-time {
    color: #8b949e;
}

.reply-info {
    color: #6e9ecf;
    font-style: italic;
}

.reply-citation {
    background: #1e2228;
    border-left: 3px solid #6e9ecf;
    border-radius: 4px;
    padding: 8px 12px;
    margin-bottom: 10px;
    font-size: 13px;
    line-height: 1.4;
}

.reply-citation-author {
    color: #6e9ecf;
    font-weight: 600;
    display: block;
    margin-bottom: 2px;
    font-size: 12px;
}

.reply-citation-content {
    color: #8b949e;
    display: block;
}

.comment-body {
    line-height: 1.5;
    color: #d1d5db;
    margin-left: 10px;
}

.loading, .error {
    text-align: center;
    padding: 60px 20px;
    color: #8b949e;
    font-style: italic;
    background: #252a31;
    border-radius: 8px;
    margin: 20px 0;
}

.error {
    color: #e0a0a0;
    background: rgba(168, 53, 53, 0.15);
}

.no-comments {
    text-align: center;
    padding: 40px;
    color: #8b949e;
    font-style: italic;
    background: #2d333b;
    border-radius: 8px;
}

.add-comment {
    margin-top: 20px;
    padding: 20px;
    background: #252a31;
    border-radius: 8px;
    border: 1px solid rgba(255, 255, 255, 0.06);
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.add-comment .form-control {
    width: 100%;
    padding: 12px 16px;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 8px;
    font-size: 14px;
    font-family: inherit;
    background: rgba(255, 255, 255, 0.04);
    color: #d1d5db;
    transition: border-color 0.3s ease, box-shadow 0.3s ease, background 0.3s ease, color 0.3s ease;
    box-sizing: border-box;
}

.add-comment .form-control:focus {
    outline: none;
    border-color: #6e9ecf;
    box-shadow: 0 0 0 2px rgba(110, 158, 207, 0.2);
    background: rgba(255, 255, 255, 0.07);
    color: #ffffff;
}

.reply-form {
    margin-top: 18px;
    width: 100%;
}

.reply-form .form-control {
    width: 100%;
    padding: 10px 14px;
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 8px;
    font-size: 14px;
    font-family: inherit;
    background: rgba(255, 255, 255, 0.04);
    color: #d1d5db;
    transition: border-color 0.3s ease, box-shadow 0.3s ease, background 0.3s ease, color 0.3s ease;
    box-sizing: border-box;
}

.reply-form .form-control:focus {
    outline: none;
    border-color: #6e9ecf;
    box-shadow: 0 0 0 2px rgba(110, 158, 207, 0.2);
    background: rgba(255, 255, 255, 0.07);
    color: #ffffff;
}

.auth-prompt {
    text-align: center;
    padding: 24px 20px;
    margin-top: 20px;
    background: #252a31;
    border-radius: 8px;
    border: 1px solid rgba(255, 255, 255, 0.06);
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.auth-prompt-icon {
    font-size: 1.5em;
    display: block;
    margin-bottom: 8px;
}

.auth-prompt p {
    color: #8b949e;
    margin: 0 0 12px 0;
    font-size: 15px;
}

.auth-prompt-links {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    color: #8b949e;
    font-size: 14px;
}

.auth-prompt-links .auth-link {
    color: #6e9ecf;
    text-decoration: none;
    font-weight: 600;
}

.auth-prompt-links .auth-link:hover {
    color: #85b1db;
    text-decoration: underline;
}

.comment-footer {
    display: flex;
    align-items: center;
    gap: 16px;
    margin-top: 8px;
}

.vote-bar {
    display: flex;
    align-items: center;
    gap: 4px;
}

.vote-btn {
    background: none;
    border: none;
    cursor: pointer;
    font-size: 11px;
    color: #555e68;
    padding: 2px 5px;
    border-radius: 3px;
    line-height: 1;
    transition: color 0.15s, background 0.15s;
}

.vote-btn.upvote:hover   { color: #3d8b5e; background: rgba(61, 139, 94, 0.1); }
.vote-btn.downvote:hover { color: #c44040; background: rgba(196, 64, 64, 0.1); }

.vote-btn.active-up   { color: #3d8b5e; }
.vote-btn.active-down { color: #c44040; }

.vote-score {
    font-size: 12px;
    font-weight: 700;
    min-width: 24px;
    text-align: center;
    font-family: 'DM Sans', sans-serif;
}

.score-positive { color: #3d8b5e; }
.score-negative { color: #c44040; }
.score-zero     { color: #555e68; }

.vote-bar-locked { cursor: default; }
.vote-btn.locked { opacity: 0.3; cursor: not-allowed; }
.vote-bar-locked:hover .vote-btn.locked { color: #555e68; background: none; }```
## File: \src\Forum.Blazor\wwwroot\app.css
```css
@import url('https://fonts.googleapis.com/css2?family=DM+Sans:wght@400;500;600;700;800&display=swap');

/* ===== CUSTOM PROPERTIES ===== */
:root {
    --bg-primary: #1c2026;
    --bg-card: #252a31;
    --bg-header: #15181d;
    --bg-elevated: #2d333b;
    --text-primary: #d1d5db;
    --text-secondary: #8b949e;
    --accent-red: #c44040;
    --accent-blue: #6e9ecf;
    --accent-blue-hover: #85b1db;
    --bg-form: rgba(255, 255, 255, 0.04);
    --border-subtle: rgba(255, 255, 255, 0.06);
    --border-input: rgba(255, 255, 255, 0.1);
}

/* ===== BASE RESET ===== */
*, *::before, *::after { box-sizing: border-box; }

html {
    scrollbar-gutter: stable;
}

html, body {
    font-family: "Helvetica Neue", Helvetica, Arial, sans-serif;
    background: var(--bg-primary);
    color: var(--text-primary);
}

a, .btn-link {
    color: var(--accent-blue);
    text-decoration: none;
}
a:hover, .btn-link:hover {
    color: var(--accent-blue-hover);
    text-decoration: underline;
}
.btn {
    padding: 12px 24px;
    border: none;
    border-radius: 6px;
    font-size: 14px;
    font-weight: 500;
    cursor: pointer;
    text-decoration: none;
    display: inline-block;
    transition: background 0.3s ease;
    text-transform: uppercase;
    letter-spacing: 1px;
}

.btn-primary {
    background: #4a7299;
    color: white;
}

.btn-primary:hover:not(:disabled) {
    background: #5a84ab;
    text-decoration: none;
    color: white;
}

.btn-primary:disabled {
    opacity: 0.6;
    cursor: not-allowed;
}

.btn-secondary {
    background: #3a4049;
    color: var(--text-primary);
}

.btn-secondary:hover {
    background: #454c56;
}
.form-control,
.form-control:disabled {
    background: var(--bg-form);
    color: var(--text-primary);
    border: 1px solid var(--border-input);
}

/* ===== Pagination dark theme (global) ===== */
.page-link {
    background: var(--bg-elevated);
    border-color: var(--border-input);
    color: var(--text-primary);
}

.page-link:hover {
    background: #373e47;
    border-color: rgba(255, 255, 255, 0.15);
    color: #ffffff;
}

.page-item.active .page-link {
    background: var(--accent-blue);
    border-color: var(--accent-blue);
    color: #ffffff;
}

.page-item.disabled .page-link {
    background: #1e2228;
    border-color: var(--border-subtle);
    color: var(--text-secondary);
}

.form-select {
    background-color: var(--bg-elevated);
    border-color: var(--border-input);
    color: var(--text-primary);
}

.btn:focus,
.btn:active:focus,
.btn-link.nav-link:focus,
.form-control:focus,
.form-check-input:focus {
    box-shadow: 0 0 0 0.1rem rgba(28, 32, 38, 0.5), 0 0 0 0.25rem rgba(110, 158, 207, 0.3);
}

.form-control:focus {
    background: rgba(255, 255, 255, 0.07);
    color: #ffffff;
    border-color: var(--accent-blue);
}
.content { padding-top: 1.1rem; }
h1:focus { outline: none; }
.valid.modified:not([type=checkbox]) { outline: 1px solid #3d8b5e; }
.invalid { outline: 1px solid var(--accent-red); }
.validation-message { color: var(--accent-red); }
.darker-border-checkbox.form-check-input { border-color: #555; }
.blazor-error-boundary {
    background: #7a2c2c;
    padding: 1rem 1rem 1rem 3.7rem;
    color: var(--text-primary);
    position: relative;
}
.blazor-error-boundary::before {
    content: "!";
    position: absolute;
    left: 1rem;
    top: 1rem;
    width: 1.8rem;
    height: 1.8rem;
    border-radius: 50%;
    background: rgba(255, 255, 255, .18);
    display: grid;
    place-items: center;
    font-weight: 800;
}
.blazor-error-boundary::after { content: "An error has occurred."; }

/* ===== SITE LAYOUT (MainLayout) ===== */
.site-wrapper {
    display: flex;
    flex-direction: column;
    min-height: 100vh;
}
.main-body {
    flex: 1;
    padding: 20px 0;
    display: flex;
    flex-direction: column;
    align-items: center;
}
.main-body > * {
    width: 100%;
}
.main-body:has(.home-wrapper) {
    padding: 0;
}
.header-container {
    max-width: 1140px;
    margin: 0 auto;
    padding: 0 15px;
    display: flex;
    align-items: center;
    justify-content: space-between;
}
.top-row-black {
    background: var(--bg-header);
    padding: 12px 0;
    color: var(--text-primary);
    border-bottom: 1px solid var(--border-subtle);
}

/* ===== BRAND (Overtime) ===== */
.brand a {
    font-family: 'DM Sans', 'Arial Black', sans-serif;
    font-size: 24px;
    font-weight: 900;
    color: var(--text-primary);
    text-decoration: none;
    letter-spacing: 10px;
    text-transform: uppercase;
}

.red-text { color: var(--accent-red); }
.brand-tagline {
    flex: 1;
    text-align: center;
    font-size: 13px;
    color: var(--text-secondary);
    font-weight: 400;
    letter-spacing: 0.5px;
    padding: 0 20px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.user-actions {
    display: flex;
    align-items: center;
}
.user-greeting {
    text-transform: none !important;
    color: var(--accent-red) !important;
    font-weight: 700;
    font-size: 18px;
    text-decoration: none !important;
    margin-right: 16px;
}
.user-greeting:hover {
    color: #fff !important;
    text-decoration: none !important;
}

/* Header search (dark) */
.top-row-black .forum-search-container { position: relative; margin-right: 15px; }
.top-row-black .search-input {
    border: 1px solid var(--border-input);
    background: var(--bg-elevated);
    color: var(--text-primary);
    padding: 5px 10px 5px 30px;
    font-size: 12px;
    width: 180px;
    outline: none;
    border-radius: 4px;
}
.top-row-black .search-input::placeholder { color: var(--text-secondary); }
.top-row-black .search-icon {
    position: absolute;
    left: 10px;
    top: 50%;
    transform: translateY(-50%);
    color: var(--text-secondary);
    font-size: 12px;
}

.btn-outline {
    background: transparent;
    border: 1px solid rgba(255, 255, 255, 0.15);
    color: var(--text-primary);
    padding: 7px 18px;
    font-weight: 700;
    font-size: 11px;
    text-transform: uppercase;
    cursor: pointer;
    margin-right: 10px;
    transition: 0.2s;
    border-radius: 3px;
}
.btn-outline:hover { background: rgba(255, 255, 255, 0.06); text-decoration: none; }
a.btn-outline { text-decoration: none; }
a.btn-outline:hover { text-decoration: none; }
a.btn-red { text-decoration: none; }
a.btn-red:hover { text-decoration: none; }
.btn-red {
    background: var(--accent-red);
    border: none;
    color: #fff;
    padding: 8px 19px;
    font-weight: 700;
    font-size: 11px;
    text-transform: uppercase;
    cursor: pointer;
    transition: 0.2s;
    border-radius: 3px;
}
.btn-red:hover { background: #a83535; }
.sub-nav-white {
    background: var(--bg-card);
    border-bottom: 3px solid var(--accent-red);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}
.nav-menu {
    list-style: none;
    display: flex;
    margin: 0;
    padding: 0;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: none;
}
.nav-menu::-webkit-scrollbar { display: none; }
.nav-menu li { flex-shrink: 0; }
.nav-menu li a {
    display: block;
    padding: 16px 20px;
    white-space: nowrap;
    color: var(--text-primary);
    text-decoration: none;
    font-weight: 800;
    text-transform: uppercase;
    font-size: 13px;
    letter-spacing: 0.5px;
    transition: color 0.15s;
}
.nav-menu li a:hover, .nav-menu li a.active { color: var(--accent-red); text-decoration: none; }
.main-footer {
    background: var(--bg-header);
    border-top: 1px solid var(--border-subtle);
    padding: 24px 0;
    color: var(--text-secondary);
    font-size: 12px;
}
.footer-content {
    flex-direction: column;
    gap: 10px;
}
.footer-content p {
    margin-bottom: 0;
}

.footer-links a {
    color: var(--text-secondary);
    text-decoration: none;
    margin: 0 5px;
}
.footer-links a:hover { color: var(--text-primary); }

/* ===== MOBILE HAMBURGER TOGGLER ===== */
.mobile-toggler {
    display: none;
    flex-direction: column;
    justify-content: center;
    gap: 5px;
    background: none;
    border: 1px solid rgba(255, 255, 255, 0.2);
    border-radius: 4px;
    padding: 7px 8px;
    cursor: pointer;
    flex-shrink: 0;
}
.mobile-toggler span {
    display: block;
    width: 20px;
    height: 2px;
    background: var(--text-primary);
    border-radius: 1px;
}
.mobile-toggler:focus {
    outline: none;
    box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.15);
}

/* ===== MOBILE COLLAPSED MENU ===== */
#navMenuCollapse {
    display: none;
    overflow: hidden;
}
@media (max-width: 991.98px) {
    #navMenuCollapse.show,
    #navMenuCollapse.collapsing {
        display: block;
    }
}

.mobile-nav-menu {
    background: var(--bg-card);
    border-bottom: 3px solid var(--accent-red);
    display: flex;
    flex-direction: column;
}

.mobile-nav-link {
    display: block;
    padding: 14px 20px;
    color: var(--text-primary);
    text-decoration: none;
    font-weight: 800;
    font-size: 13px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    border-bottom: 1px solid var(--border-subtle);
    transition: color 0.15s;
}

.mobile-nav-link:hover {
    color: var(--accent-red);
    text-decoration: none;
}

.mobile-nav-btn {
    background: none;
    border: none;
    border-bottom: 1px solid var(--border-subtle);
    cursor: pointer;
    text-align: left;
    width: 100%;
    font-family: inherit;
}

.mobile-nav-auth {
    background: rgba(74, 114, 153, 0.15);
    border-top: 1px solid rgba(110, 158, 207, 0.2);
}

/* ===== RESPONSIVE BREAKPOINT ===== */
.desktop-only { display: flex; }

@media (max-width: 991.98px) {
    .desktop-only { display: none !important; }
    .mobile-toggler { display: flex; }
    .brand-tagline { display: none; }
    .brand { flex: 1; text-align: center; }
    .brand a { font-size: 20px; letter-spacing: 6px; }
}

```
## File: \src\Forum.Blazor\Program.cs
```cs
using Forum.Blazor.Components;
using Forum.Blazor.Interfaces;
using Forum.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register named HTTP client pointing to the API
builder.Services.AddHttpClient("ForumApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5141");
});

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5141"),
    Timeout = TimeSpan.FromSeconds(15)
});

// Authentication and authorization services
builder.Services.AddScoped<ITokenStorageService, TokenStorageService>();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// API service clients
builder.Services.AddScoped<IAuthClientService, AuthService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<ThreadService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<UserService>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
```
## File: \src\Forum.Domain\Entities\Category.cs
```cs
namespace Forum.Domain.Entities;

/// <summary>
/// Forum category that groups related threads (e.g. "Formula 1", "Football").
/// </summary>
public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
}
```
## File: \src\Forum.Domain\Entities\Comment.cs
```cs
namespace Forum.Domain.Entities;

/// <summary>
/// A comment within a thread. Supports soft-delete, self-referencing replies via ParentCommentId,
/// and upvote/downvote scoring via Votes.
/// </summary>
public class Comment
{
    public int CommentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime TimeCreated { get; set; }
    public bool IsDeleted { get; set; }

    // Foreign keys
    public string UserId { get; set; } = string.Empty;
    public int ThreadId { get; set; }
    public int? ParentCommentId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Thread Thread { get; set; } = null!;
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();

}
```
## File: \src\Forum.Domain\Entities\Thread.cs
```cs
namespace Forum.Domain.Entities;

/// <summary>
/// A discussion thread within a category. The thread body is stored as the first comment.
/// </summary>
public class Thread
{
    public int ThreadId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    // Foreign keys
    public string UserId { get; set; } = string.Empty;
    public int CategoryId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
```
## File: \src\Forum.Domain\Entities\User.cs
```cs
using Microsoft.AspNetCore.Identity;

namespace Forum.Domain.Entities;

/// <summary>
/// Forum user, extends ASP.NET Core Identity with soft-delete and navigation properties.
/// </summary>
public class User : IdentityUser
{
    public bool IsDeleted { get; set; }
    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
```
## File: \src\Forum.Domain\Entities\Vote.cs
```cs
namespace Forum.Domain.Entities;

/// <summary>
/// An upvote (+1) or downvote (-1) cast by a user on a comment.
/// Each user may vote only once per comment (enforced by a unique index).
/// </summary>
public class Vote
{
    public int VoteId { get; set; }          // Primary key
    public int Value { get; set; }           // 1 for upvote, -1 for downvote
    public string UserId { get; set; } = string.Empty; // FK to the user who cast the vote
    public int CommentId { get; set; }       // FK to the comment being voted on
    public User User { get; set; } = null!;  // Navigation property to the voting user
    public Comment Comment { get; set; } = null!; // Navigation property to the voted-on comment
}```
## File: \src\Forum.Infrastructure\Data\DbSeeder.cs
```cs
using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
// Specify full name for Thread to avoid conflict with System.Threading.Thread
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Infrastructure.Data;

/// <summary>
/// Seeds the database with initial roles, users, categories, threads, and comments.
/// Skips seeding if any users already exist to prevent duplicate data.
/// </summary>
public static class DbSeeder
{
    // Entry point that seeds roles, users, categories, threads, and comments.
    public static async Task SeedAsync(ForumDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Skip seeding if any users already exist to avoid duplicate data.
        if (await context.Users.AnyAsync())
        {
            return;
        }

        // Seed roles, then users, categories, threads and comments in that order.
        await SeedRolesAsync(roleManager);
        var (adminUser, regularUser, user2) = await SeedUsersAsync(userManager);
        var categories = await SeedCategoriesAsync(context);
        var threads = await SeedThreadsAsync(context, adminUser, regularUser, user2, categories);
        await SeedCommentsAsync(context, adminUser, regularUser, user2, threads);

        // Persist any remaining changes to the database.
        await context.SaveChangesAsync();
    }

    // Ensure required role "Admin" exist.
    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }
    }

    // Create initial users and assign appropriate roles.
    private static async Task<(User Admin, User Regular, User User2)> SeedUsersAsync(UserManager<User> userManager)
    {
        var adminUser = new User
        {
            UserName = "admin",
            Email = "admin@forum.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");

        var regularUser = new User
        {
            UserName = "john_doe",
            Email = "john@example.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(regularUser, "User123!");

        var user2 = new User
        {
            UserName = "jane_smith",
            Email = "jane@example.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user2, "User123!");

        return (adminUser, regularUser, user2);
    }

    // Create a set of default forum categories.
    private static async Task<List<Category>> SeedCategoriesAsync(ForumDbContext context)
    {
        // Prepare a list of default categories.
        var categories = new List<Category>
        {
            new() { Name = "Formula 1" },
            new() { Name = "Football" },
            new() { Name = "Basketball" },
            new() { Name = "Tennis" },
            new() { Name = "Hockey" },
            new() { Name = "Golf" },
            new() { Name = "Cycling" },
            new() { Name = "Rugby" }
        };

        // Add the categories to the context and save so they receive keys.
        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        return categories;
    }

    // Create sample threads associated with seeded users and categories.
    private static async Task<List<ThreadEntity>> SeedThreadsAsync
    (
        ForumDbContext context,
        User adminUser,
        User regularUser,
        User user2,
        List<Category> categories)
    {
        // Sample Thread including their title, id created by and when they were created and updated.
        var threads = new List<ThreadEntity>
        {
            // Formula 1
            new()
            {
                Title = "2025 Season Predictions - Who takes the championship?",
                UserId = regularUser.Id,
                CategoryId = categories[0].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-10),
                TimeUpdated = DateTime.UtcNow.AddDays(-10)
            },
            // Football
            new()
            {
                Title = "Champions League Semi-Finals Discussion",
                UserId = user2.Id,
                CategoryId = categories[1].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-8),
                TimeUpdated = DateTime.UtcNow.AddDays(-7)
            },
            // Basketball
            new()
            {
                Title = "NBA Playoffs - Who's making it out of the West?",
                UserId = regularUser.Id,
                CategoryId = categories[2].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-6),
                TimeUpdated = DateTime.UtcNow.AddDays(-5)
            },
            // Tennis
            new()
            {
                Title = "Is Sinner the new GOAT?",
                UserId = user2.Id,
                CategoryId = categories[3].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-5),
                TimeUpdated = DateTime.UtcNow.AddDays(-4)
            },
            // Hockey
            new()
            {
                Title = "Stanley Cup contenders this year",
                UserId = adminUser.Id,
                CategoryId = categories[4].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-4),
                TimeUpdated = DateTime.UtcNow.AddDays(-3)
            },
            // Golf
            new()
            {
                Title = "Masters 2025 - Early favorites?",
                UserId = regularUser.Id,
                CategoryId = categories[5].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-3),
                TimeUpdated = DateTime.UtcNow.AddDays(-2)
            },
            // Cycling
            new()
            {
                Title = "Tour de France route looks insane this year",
                UserId = user2.Id,
                CategoryId = categories[6].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-2),
                TimeUpdated = DateTime.UtcNow.AddDays(-1)
            },
            // Rugby
            new()
            {
                Title = "Six Nations 2025 - Ireland vs France was incredible",
                UserId = adminUser.Id,
                CategoryId = categories[7].CategoryId,
                TimeCreated = DateTime.UtcNow.AddDays(-1),
                TimeUpdated = DateTime.UtcNow.AddDays(-1)
            }
        };
        // Add threads to the context and save to generate keys.
        await context.Threads.AddRangeAsync(threads);
        await context.SaveChangesAsync();

        return threads;
    }

    // Create sample comments and a reply to demonstrate comment threading.
    private static async Task SeedCommentsAsync
    (
        ForumDbContext context,
        User adminUser,
        User regularUser,
        User user2,
        List<ThreadEntity> threads)
    {
        // Prepare a list of sample comments tied to threads and users.
        var comments = new List<Comment>
        {
            // Thread 0: F1 - body comment
            new()
            {
                Content = "With the new regulations shaking things up, who do you think takes the 2025 Drivers' Championship? I'm leaning towards Verstappen again but Norris is looking seriously quick.",
                UserId = regularUser.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-10)
            },
            // Thread 0: F1 - reply
            new()
            {
                Content = "McLaren have the best car right now. Norris finally has the machinery to fight for it. My money is on him.",
                UserId = user2.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-9)
            },
            // Thread 0: F1 - reply
            new()
            {
                Content = "Never count out Verstappen. He always finds another gear when it matters most.",
                UserId = adminUser.Id,
                ThreadId = threads[0].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-9).AddHours(2)
            },
            // Thread 1: Football - body comment
            new()
            {
                Content = "The Champions League semis are set! Real Madrid vs Arsenal and Barcelona vs Bayern. What are your predictions?",
                UserId = user2.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-8)
            },
            // Thread 1: Football - reply
            new()
            {
                Content = "Arsenal finally have the squad depth to go all the way. Saka has been unreal this season.",
                UserId = regularUser.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-7)
            },
            // Thread 1: Football - reply
            new()
            {
                Content = "Real Madrid in the Champions League is a different beast. You just can't bet against them at the Bernabeu.",
                UserId = adminUser.Id,
                ThreadId = threads[1].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-7).AddHours(3)
            },
            // Thread 2: Basketball - body comment
            new()
            {
                Content = "The Western Conference is stacked this year. Thunder, Nuggets, Wolves, and the Mavs all look dangerous. Who's your pick to make the Finals?",
                UserId = regularUser.Id,
                ThreadId = threads[2].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-6)
            },
            // Thread 2: Basketball - reply
            new()
            {
                Content = "OKC Thunder all the way. SGA is playing at an MVP level and their defense is elite.",
                UserId = user2.Id,
                ThreadId = threads[2].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-5)
            },
            // Thread 3: Tennis - body comment
            new()
            {
                Content = "Sinner has been dominating the hard courts and looking untouchable. With Djokovic winding down, is Sinner the next GOAT in the making?",
                UserId = user2.Id,
                ThreadId = threads[3].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-5)
            },
            // Thread 3: Tennis - reply
            new()
            {
                Content = "He's incredible but let's not forget Alcaraz. Those two are going to have an epic rivalry for years to come.",
                UserId = regularUser.Id,
                ThreadId = threads[3].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-4)
            },
            // Thread 4: Hockey - body comment
            new()
            {
                Content = "Who are your top Stanley Cup contenders? I think the Panthers are looking to repeat and Edmonton is hungry after last year's final loss.",
                UserId = adminUser.Id,
                ThreadId = threads[4].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-4)
            },
            // Thread 4: Hockey - reply
            new()
            {
                Content = "The Rangers have been quietly building something special. Shesterkin is a wall and their offense is clicking.",
                UserId = regularUser.Id,
                ThreadId = threads[4].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-3)
            },
            // Thread 5: Golf - body comment
            new()
            {
                Content = "Augusta is right around the corner. Who are your early picks to win the green jacket this year? Scheffler has to be the favorite.",
                UserId = regularUser.Id,
                ThreadId = threads[5].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-3)
            },
            // Thread 5: Golf - reply
            new()
            {
                Content = "Scheffler is the obvious pick but watch out for Rory. He's due for a Masters win and has been playing incredible golf.",
                UserId = user2.Id,
                ThreadId = threads[5].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-2)
            },
            // Thread 6: Cycling - body comment
            new()
            {
                Content = "Have you seen the Tour de France route? Multiple mountain stages back to back. This is going to be a war of attrition. Pogacar vs Vingegaard round 4!",
                UserId = user2.Id,
                ThreadId = threads[6].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-2)
            },
            // Thread 6: Cycling - reply
            new()
            {
                Content = "Pogacar is on another level right now. After winning the Giro and Tour double last year, he's the clear favorite.",
                UserId = adminUser.Id,
                ThreadId = threads[6].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-1)
            },
            // Thread 7: Rugby - body comment
            new()
            {
                Content = "What a match! Ireland vs France in the Six Nations was an absolute classic. Ireland's defense in the last 10 minutes was heroic.",
                UserId = adminUser.Id,
                ThreadId = threads[7].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-1)
            },
            // Thread 7: Rugby - reply
            new()
            {
                Content = "France were brilliant in attack but Ireland just know how to win these tight games. Grand Slam contenders for sure.",
                UserId = regularUser.Id,
                ThreadId = threads[7].ThreadId,
                TimeCreated = DateTime.UtcNow.AddDays(-1).AddHours(3)
            }
        };
        // Create a reply linked to the second comment to demonstrate parent/child.
        var firstCommentWithReply = comments[1];
        var replyToFirstComment = new Comment
        {
            Content = "Exactly! The McLaren upgrades have been spot on all season.",
            UserId = regularUser.Id,
            ThreadId = threads[0].ThreadId,
            ParentCommentId = firstCommentWithReply.CommentId,
            TimeCreated = DateTime.UtcNow.AddDays(-9).AddHours(1)
        };
        // Add initial comments and save so IDs are generated before adding replies.
        await context.Comments.AddRangeAsync(comments);
        await context.SaveChangesAsync();

        replyToFirstComment.ParentCommentId = firstCommentWithReply.CommentId;
        await context.Comments.AddAsync(replyToFirstComment);
        await context.SaveChangesAsync();
    }
}```
## File: \src\Forum.Infrastructure\Data\ForumContextFactory.cs
```cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forum.Infrastructure.Data;

/// <summary>
/// Factory used by EF Core CLI tools (e.g. dotnet ef migrations)
/// to create a ForumDbContext when the application host is not running.
/// </summary>
public class ForumDbContextFactory
    : IDesignTimeDbContextFactory<ForumDbContext>
{
    public ForumDbContext CreateDbContext(string[] args)
    {
        var solutionRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
        var dbPath = Path.Combine(solutionRoot, "forum.db");

        var optionsBuilder = new DbContextOptionsBuilder<ForumDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new ForumDbContext(optionsBuilder.Options);
    }
}
```
## File: \src\Forum.Infrastructure\Data\ForumDbContext.cs
```cs
using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Data;

/// <summary>
/// EF Core database context for the forum, extending IdentityDbContext for authentication.
/// Configures entity relationships, constraints, and indexes in OnModelCreating.
/// </summary>
public class ForumDbContext : IdentityDbContext<User>
{
    public ForumDbContext(DbContextOptions<ForumDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();

    // Specify Full Name for Thread to avoid conflict with System.Threading.Thread
    public DbSet<Domain.Entities.Thread> Threads => Set<Domain.Entities.Thread>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Vote> Votes => Set<Vote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // CATEGORY
        builder.Entity<Category>(e =>
        {
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
        });

        // THREAD
        builder.Entity<Domain.Entities.Thread>(e =>
        {
            e.HasKey(x => x.ThreadId);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.TimeCreated).IsRequired();
            e.Property(x => x.TimeUpdated).IsRequired();

            // Thread -> User (author). Restrict delete to avoid cascading user deletion of threads.
            e.HasOne(x => x.User)
                .WithMany(u => u.Threads)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Thread -> Category. Restrict delete to avoid cascading category deletion.
            e.HasOne(x => x.Category)
                .WithMany(c => c.Threads)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // COMMENT
        builder.Entity<Comment>(e =>
        {
            e.HasKey(x => x.CommentId);
            e.Property(x => x.Content).IsRequired();
            e.Property(x => x.TimeCreated).IsRequired();

            // Comment -> User (author). Restrict delete to preserve historical data.
            e.HasOne(x => x.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Comment -> Thread. Cascade delete comments when thread is removed.
            e.HasOne(x => x.Thread)
                .WithMany(t => t.Comments)
                .HasForeignKey(x => x.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            // per parent comment has many replies
            e.HasOne(x => x.ParentComment)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentCommentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // VOTE
        builder.Entity<Vote>(e =>
        {
            e.HasKey(x => x.VoteId);
            e.Property(x => x.Value).IsRequired();

            // Vote -> User. Cascade deletes votes when user removed.
            e.HasOne(x => x.User)
                .WithMany(u => u.Votes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Vote -> Comment. Cascade deletes votes when comment removed.
            e.HasOne(x => x.Comment)
                .WithMany(c => c.Votes)
                .HasForeignKey(x => x.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            // one vote per user per comment
            e.HasIndex(x => new { x.UserId, x.CommentId }).IsUnique();
        });
    }
}```
## File: \src\Forum.Infrastructure\Migrations\20260209154100_InitialCreate.cs
```cs
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Threads",
                columns: table => new
                {
                    ThreadId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TimeCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TimeUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Threads", x => x.ThreadId);
                    table.ForeignKey(
                        name: "FK_Threads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Threads_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    CommentId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    TimeCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentCommentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.CommentId);
                    table.ForeignKey(
                        name: "FK_Comments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Comments_Comments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Comments_Threads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "Threads",
                        principalColumn: "ThreadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ThreadId",
                table: "Comments",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_CategoryId",
                table: "Threads",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Threads_UserId",
                table: "Threads",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Threads");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
```
## File: \src\Forum.Infrastructure\Migrations\20260217143229_AddIsDeletedColumns.cs
```cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Comments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AspNetUsers");
        }
    }
}
```
## File: \src\Forum.Infrastructure\Migrations\20260222220648_ChangeParentCommentDeleteBehaviorToSetNull.cs
```cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeParentCommentDeleteBehaviorToSetNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId",
                principalTable: "Comments",
                principalColumn: "CommentId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
```
## File: \src\Forum.Infrastructure\Migrations\20260308000611_AddVoteEntity.cs
```cs
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forum.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoteEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    VoteId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Value = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CommentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.VoteId);
                    table.ForeignKey(
                        name: "FK_Votes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "CommentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_CommentId",
                table: "Votes",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId_CommentId",
                table: "Votes",
                columns: new[] { "UserId", "CommentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Votes");
        }
    }
}
```
## File: \src\Forum.Infrastructure\Migrations\ForumDbContextModelSnapshot.cs
```cs
// <auto-generated />
using System;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Forum.Infrastructure.Migrations
{
    [DbContext(typeof(ForumDbContext))]
    partial class ForumDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.23");

            modelBuilder.Entity("Forum.Domain.Entities.Category", b =>
                {
                    b.Property<int>("CategoryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.HasKey("CategoryId");

                    b.ToTable("Categories");
                });

            modelBuilder.Entity("Forum.Domain.Entities.Comment", b =>
                {
                    b.Property<int>("CommentId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ParentCommentId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("ThreadId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("TimeCreated")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("CommentId");

                    b.HasIndex("ParentCommentId");

                    b.HasIndex("ThreadId");

                    b.HasIndex("UserId");

                    b.ToTable("Comments");
                });

            modelBuilder.Entity("Forum.Domain.Entities.Thread", b =>
                {
                    b.Property<int>("ThreadId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("CategoryId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("TimeCreated")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("TimeUpdated")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("ThreadId");

                    b.HasIndex("CategoryId");

                    b.HasIndex("UserId");

                    b.ToTable("Threads");
                });

            modelBuilder.Entity("Forum.Domain.Entities.User", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("TEXT");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("TEXT");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("TEXT");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex");

                    b.ToTable("AspNetUsers", (string)null);
                });

            modelBuilder.Entity("Forum.Domain.Entities.Vote", b =>
                {
                    b.Property<int>("VoteId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("CommentId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Value")
                        .HasColumnType("INTEGER");

                    b.HasKey("VoteId");

                    b.HasIndex("CommentId");

                    b.HasIndex("UserId", "CommentId")
                        .IsUnique();

                    b.ToTable("Votes");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex");

                    b.ToTable("AspNetRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClaimType")
                        .HasColumnType("TEXT");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("TEXT");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClaimType")
                        .HasColumnType("TEXT");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderKey")
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("RoleId")
                        .HasColumnType("TEXT");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("TEXT");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens", (string)null);
                });

            modelBuilder.Entity("Forum.Domain.Entities.Comment", b =>
                {
                    b.HasOne("Forum.Domain.Entities.Comment", "ParentComment")
                        .WithMany("Replies")
                        .HasForeignKey("ParentCommentId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("Forum.Domain.Entities.Thread", "Thread")
                        .WithMany("Comments")
                        .HasForeignKey("ThreadId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Forum.Domain.Entities.User", "User")
                        .WithMany("Comments")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("ParentComment");

                    b.Navigation("Thread");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Forum.Domain.Entities.Thread", b =>
                {
                    b.HasOne("Forum.Domain.Entities.Category", "Category")
                        .WithMany("Threads")
                        .HasForeignKey("CategoryId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Forum.Domain.Entities.User", "User")
                        .WithMany("Threads")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Category");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Forum.Domain.Entities.Vote", b =>
                {
                    b.HasOne("Forum.Domain.Entities.Comment", "Comment")
                        .WithMany("Votes")
                        .HasForeignKey("CommentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Forum.Domain.Entities.User", "User")
                        .WithMany("Votes")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Comment");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("Forum.Domain.Entities.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("Forum.Domain.Entities.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Forum.Domain.Entities.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("Forum.Domain.Entities.User", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Forum.Domain.Entities.Category", b =>
                {
                    b.Navigation("Threads");
                });

            modelBuilder.Entity("Forum.Domain.Entities.Comment", b =>
                {
                    b.Navigation("Replies");

                    b.Navigation("Votes");
                });

            modelBuilder.Entity("Forum.Domain.Entities.Thread", b =>
                {
                    b.Navigation("Comments");
                });

            modelBuilder.Entity("Forum.Domain.Entities.User", b =>
                {
                    b.Navigation("Comments");

                    b.Navigation("Threads");

                    b.Navigation("Votes");
                });
#pragma warning restore 612, 618
        }
    }
}
```
## File: \src\Forum.Infrastructure\Repositories\CategoryRepository.cs
```cs
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for Category entities.
/// Extending the generic Repository{T} with category-specific query methods.
/// </summary>
public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    /// <summary>
    /// Initializes a new instance of the CategoryRepository class.
    /// </summary>
    public CategoryRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves a category by its ID, eagerly loading its associated threads and each thread's author.
    /// </summary>
    public async Task<Category?> GetByIdWithThreadsAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Threads)
                .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId, cancellationToken);
    }

    /// <summary>
    /// Retrieves all categories with their threads eagerly loaded.
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAllWithThreadCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Threads)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether a category with the specified name already exists, optionally
    /// excluding a specific category (used when updating a category's name).
    /// </summary>
    public async Task<bool> NameExistsAsync(string name, int? excludeCategoryId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(c => c.Name == name);
        
        if (excludeCategoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId != excludeCategoryId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}```
## File: \src\Forum.Infrastructure\Repositories\CommentRepository.cs
```cs
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Comment;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for Comment entities.
/// Extending the generic Repository{T} with comment-specific query methods.
/// </summary>
public class CommentRepository : Repository<Comment>, ICommentRepository
{
    /// <summary>
    /// Initializes a new instance of the CommentRepository class.
    /// </summary>
    public CommentRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves a comment by its ID with full details.
    /// </summary>
    public async Task<Comment?> GetByIdWithDetailsAsync(int commentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.User)
            .Include(c => c.Thread)
            .Include(c => c.ParentComment)
                .ThenInclude(pc => pc!.User)
            .FirstOrDefaultAsync(c => c.CommentId == commentId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a paginated list of comments for a specific thread, ordered by creation time ascending.
    /// Soft-deleted comments are included only if they have replies, preserving the reply chain.
    /// </summary>
    public async Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedByThreadIdAsync(
        int threadId,
        PaginationParams paginationParams,
        CancellationToken cancellationToken = default)
    {
        // Find the body comment ID (oldest comment in the thread) so we can exclude it.
        // The first comment serves as the thread body and is displayed separately.
        var bodyCommentId = await DbSet
            .Where(c => c.ThreadId == threadId)
            .OrderBy(c => c.TimeCreated)
            .Select(c => (int?)c.CommentId)
            .FirstOrDefaultAsync(cancellationToken);

        var query = DbSet
            .Include(c => c.User)
            .Include(c => c.ParentComment)
                .ThenInclude(pc => pc!.User)
            .Where(c => c.ThreadId == threadId && (!c.IsDeleted || c.Replies.Any()))
            .Where(c => c.CommentId != bodyCommentId)
            .OrderBy(c => c.TimeCreated);
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
            .Take(paginationParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a paginated, filtered list of comments across all threads.
    /// Supports filtering by author, thread, and date range. Excludes soft-deleted comments.
    /// </summary>
    public async Task<(IReadOnlyList<Comment> Items, int TotalCount)> GetPagedAsync(
        CommentFilterParams filterParams,
        CancellationToken cancellationToken = default)
    {
        // Exclude thread body comments (the oldest comment in each thread serves as the thread body)
        var bodyCommentIds = DbSet
            .GroupBy(c => c.ThreadId)
            .Select(g => g.OrderBy(c => c.TimeCreated).Select(c => c.CommentId).First());

        var query = DbSet
            .Include(c => c.User)
            .Include(c => c.Thread)
            .Where(c => !c.IsDeleted)
            .Where(c => !bodyCommentIds.Contains(c.CommentId))
            .AsQueryable();
        
        if (!string.IsNullOrWhiteSpace(filterParams.AuthorId))
        {
            query = query.Where(c => c.UserId == filterParams.AuthorId);
        }
        
        if (filterParams.ThreadId.HasValue)
        {
            query = query.Where(c => c.ThreadId == filterParams.ThreadId.Value);
        }
        
        if (filterParams.FromDate.HasValue)
        {
            query = query.Where(c => c.TimeCreated >= filterParams.FromDate.Value);
        }
        
        if (filterParams.ToDate.HasValue)
        {
            query = query.Where(c => c.TimeCreated <= filterParams.ToDate.Value);
        }
        
        query = filterParams.SortBy switch
        {
            CommentSortBy.Oldest => query.OrderBy(c => c.TimeCreated),
            _ => query.OrderByDescending(c => c.TimeCreated)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
            .Take(filterParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves the most recent comments made by a specific user, ordered by creation time descending.
    /// Includes the Thread for each comment.
    /// </summary>
    public async Task<IReadOnlyList<Comment>> GetByUserIdAsync(string userId, int count, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Thread)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.TimeCreated)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves the oldest comment in a thread, aka the thread body.
    /// Includes the comment's author.
    /// </summary>
    public async Task<Comment?> GetFirstCommentByThreadIdAsync(int threadId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.User)
            .Where(c => c.ThreadId == threadId)
            .OrderBy(c => c.TimeCreated)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all direct replies to a specific comment, ordered by creation time ascending.
    /// Includes the author of each reply.
    /// </summary>
    public async Task<IReadOnlyList<Comment>> GetRepliesAsync(int commentId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.User)
            .Where(c => c.ParentCommentId == commentId)
            .OrderBy(c => c.TimeCreated)
            .ToListAsync(cancellationToken);
    }
}```
## File: \src\Forum.Infrastructure\Repositories\Repository.cs
```cs
using System.Linq.Expressions;
using Forum.Application.Repositories;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Generic base repository that provides standard CRUD operations for any entity type.
/// Concrete repositories inherit from this class and add entity-specific query methods.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    /// <summary>
    /// The database context shared across all repositories within a unit of work.
    /// </summary>
    protected readonly ForumDbContext Context;

    /// <summary>
    /// The EF Core DbSet{T} for the entity type.
    /// </summary>
    protected readonly DbSet<T> DbSet;

    /// <summary>
    /// Initializes a new instance of the Repository{T} class.
    /// </summary>
    public Repository(ForumDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    /// <summary>
    /// Retrieves a single entity by its primary key using DbSet{T}.FindAsync.
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new[] { id }, cancellationToken);
    }

    /// <summary>
    /// Retrieves all entities of type T from the database.
    /// </summary>
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all entities matching the specified predicate.
    /// </summary>
    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new entity to the DbSet.
    /// </summary>
    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Marks an existing entity as modified.
    /// </summary>
    public virtual void Update(T entity)
    {
        DbSet.Update(entity);
    }

    /// <summary>
    /// Marks an entity for removal from the database (hard delete).
    /// </summary>
    public virtual void Delete(T entity)
    {
        DbSet.Remove(entity);
    }

    /// <summary>
    /// Checks whether any entity matching the specified predicate exists in the database.
    /// </summary>
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    /// <summary>
    /// Counts the number of entities matching an optional predicate.
    /// If no predicate is provided, counts all entities of type T.
    /// </summary>
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        if (predicate == null)
        {
            return await DbSet.CountAsync(cancellationToken);
        }
        return await DbSet.CountAsync(predicate, cancellationToken);
    }
}

```
## File: \src\Forum.Infrastructure\Repositories\ThreadRepository.cs
```cs
using Forum.Application.DTOs.Thread;
using Forum.Application.Repositories;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ThreadEntity = Forum.Domain.Entities.Thread;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for ThreadEntity.
/// Extending the generic Repository{T}> with thread-specific query methods.
/// </summary>
public class ThreadRepository : Repository<ThreadEntity>, IThreadRepository
{
    /// <summary>
    /// Initializes a new instance of the ThreadRepository class.
    /// </summary>
    public ThreadRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves a thread by its ID with the author (User) and Category eagerly loaded (without comments).
    /// </summary>
    public async Task<ThreadEntity?> GetByIdWithDetailsAsync(int threadId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.User)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.ThreadId == threadId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a thread by its ID with full details: author, category, and all comments.
    /// Soft-deleted comments are included only if they have replies, preserving the reply chain.
    /// </summary>
    public async Task<ThreadEntity?> GetByIdWithCommentsAsync(int threadId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.User)
            .Include(t => t.Category)
            .Include(t => t.Comments.Where(c => !c.IsDeleted || c.Replies.Any()))
                .ThenInclude(c => c.User)
            .Include(t => t.Comments)
                .ThenInclude(c => c.ParentComment)
            .FirstOrDefaultAsync(t => t.ThreadId == threadId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a paginated, filtered, and sorted list of threads.
    /// </summary>
    public async Task<(IReadOnlyList<ThreadEntity> Items, int TotalCount)> GetPagedAsync(
        ThreadFilterParams filterParams,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(t => t.User)
            .Include(t => t.Category)
            .Include(t => t.Comments)
                .ThenInclude(c => c.User)
            .AsQueryable();
        
        if (filterParams.CategoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == filterParams.CategoryId.Value);
        }
        
        if (!string.IsNullOrWhiteSpace(filterParams.AuthorId))
        {
            query = query.Where(t => t.UserId == filterParams.AuthorId);
        }
        
        if (!string.IsNullOrWhiteSpace(filterParams.SearchTerm))
        {
            query = query.Where(t => t.Title.Contains(filterParams.SearchTerm));
        }
        
        if (filterParams.FromDate.HasValue)
        {
            query = query.Where(t => t.TimeCreated >= filterParams.FromDate.Value);
        }
        
        if (filterParams.ToDate.HasValue)
        {
            query = query.Where(t => t.TimeCreated <= filterParams.ToDate.Value);
        }
        
        query = filterParams.SortBy switch
        {
            ThreadSortBy.Oldest => query.OrderBy(t => t.TimeCreated),
            ThreadSortBy.RecentlyUpdated => query.OrderByDescending(t => t.TimeUpdated),
            ThreadSortBy.MostComments => query.OrderByDescending(t => t.Comments.Count),
            ThreadSortBy.Category => query.OrderBy(t => t.Category.Name),
            ThreadSortBy.Author => query.OrderBy(t => t.User.UserName),
            _ => query.OrderByDescending(t => t.TimeCreated)
        };
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
            .Take(filterParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves the most recent threads created by a specific user, ordered by creation time.
    /// Includes the Category for each thread.
    /// </summary>
    public async Task<IReadOnlyList<ThreadEntity>> GetByUserIdAsync(string userId, int count, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TimeCreated)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves all threads belonging to a specific category, ordered by most recently updated.
    /// Includes the thread author.
    /// </summary>
    public async Task<IReadOnlyList<ThreadEntity>> GetByCategoryIdAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.User)
            .Where(t => t.CategoryId == categoryId)
            .OrderByDescending(t => t.TimeUpdated)
            .ToListAsync(cancellationToken);
    }
}```
## File: \src\Forum.Infrastructure\Repositories\UnitOfWork.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Infrastructure.Data;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Implements the Unit of Work pattern by wrapping ForumDbContext.SaveChangesAsync.
/// Ensures that all repository operations within a single request are committed as a single transaction.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ForumDbContext _context;

    /// <summary>
    /// Initializes a new instance of the UnitOfWork class.
    /// </summary>
    public UnitOfWork(ForumDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Persists all pending changes tracked by the underlying ForumDbContext to the database,
    /// in a single transaction.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}```
## File: \src\Forum.Infrastructure\Repositories\UserRepository.cs
```cs
using Forum.Application.DTOs.User;
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// Repository for User entities.
/// Extending the generic Repository{T} with user-specific query methods.
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    /// <summary>
    /// Initializes a new instance of the UserRepository class.
    /// </summary>
    public UserRepository(ForumDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Retrieves all users with their threads and comments eagerly loaded for count display.
    /// </summary>
    public override async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Threads)
            .Include(u => u.Comments)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a user by their ID with their threads and comments eagerly loaded.
    /// For user profile views that display activity statistics.
    /// </summary>
    public async Task<User?> GetByIdWithDetailsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(u => u.Threads)
            .Include(u => u.Comments)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
    }

    /// <summary>
    /// Checks whether a username is already taken, optionally excluding a specific user
    /// (when a user is updating their username).
    /// </summary>
    public async Task<bool> UserNameExistsAsync(string userName, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(u => u.UserName == userName);
        
        if (!string.IsNullOrWhiteSpace(excludeUserId))
        {
            query = query.Where(u => u.Id != excludeUserId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether an email address is already in use, optionally excluding a specific user
    /// (when a user is updating their email).
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, string? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(u => u.Email == email);

        if (!string.IsNullOrWhiteSpace(excludeUserId))
        {
            query = query.Where(u => u.Id != excludeUserId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a paginated list of users with their threads and comments eagerly loaded.
    /// Supports sorting by username, thread count, or comment count.
    /// </summary>
    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        UserFilterParams filterParams,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = DbSet
            .Include(u => u.Threads)
            .Include(u => u.Comments);

        IQueryable<User> query = filterParams.SortBy switch
        {
            UserSortBy.Threads => baseQuery.OrderByDescending(u => u.Threads.Count),
            UserSortBy.Comments => baseQuery.OrderByDescending(u => u.Comments.Count),
            _ => baseQuery.OrderBy(u => u.UserName)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((filterParams.PageNumber - 1) * filterParams.PageSize)
            .Take(filterParams.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}

```
## File: \src\Forum.Infrastructure\Repositories\VoteRepository.cs
```cs
using Forum.Application.Repositories;
using Forum.Domain.Entities;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Forum.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IVoteRepository for managing comment votes.
/// </summary>
public class VoteRepository : Repository<Vote>, IVoteRepository
{
    public VoteRepository(ForumDbContext context) : base(context) { }

    /// <summary>
    /// Returns the vote cast by a specific user on a specific comment, or null if no vote exists.
    /// </summary>
    public async Task<Vote?> GetByUserAndCommentAsync(string userId, int commentId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(v => v.UserId == userId && v.CommentId == commentId, ct);
    }

    /// <summary>
    /// Returns the net vote score for each comment, keyed by CommentId.
    /// </summary>
    public async Task<Dictionary<int, int>> GetScoresForCommentsAsync(IEnumerable<int> commentIds, CancellationToken ct = default)
    {
        var ids = commentIds.ToList();
        return await DbSet
            .Where(v => ids.Contains(v.CommentId))
            .GroupBy(v => v.CommentId)
            .Select(g => new { CommentId = g.Key, Score = g.Sum(v => v.Value) })
            .ToDictionaryAsync(x => x.CommentId, x => x.Score, ct);
    }

    /// <summary>
    /// Returns the current user's vote value for each comment, keyed by CommentId.
    /// </summary>
    public async Task<Dictionary<int, int>> GetUserVotesForCommentsAsync(string userId, IEnumerable<int> commentIds, CancellationToken ct = default)
    {
        var ids = commentIds.ToList();
        return await DbSet
            .Where(v => v.UserId == userId && ids.Contains(v.CommentId))
            .ToDictionaryAsync(v => v.CommentId, v => v.Value, ct);
    }
}```
## File: \src\Forum.Infrastructure\Services\AuthService.cs
```cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Forum.Application.Common.Interfaces;
using Forum.Application.Common.Models;
using Forum.Application.DTOs.Auth;
using Forum.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Forum.Infrastructure.Services;

/// <summary>
/// Implements authentication operations including user registration,
/// login with JWT token generation, and logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the AuthService class.
    /// </summary>
    public AuthService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    /// <summary>
    /// Registers a new user with the provided credentials using Identity.
    /// </summary>
    public async Task<Result<object>> RegisterAsync(string username, string email, string password)
    {
        var user = new User
        {
            UserName = username,
            Email = email
        };

        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure<object>(errors);
        }

        return Result.Success<object>(new { Message = "User registered successfully", UserId = user.Id });
    }

    /// <summary>
    /// Authenticates a user by validating their credentials and generates a JWT token on success.
    /// Rejects soft-deleted users and enforces server-side lockout.
    /// </summary>
    public async Task<Result<AuthResponse>> LoginAsync(string username, string password)
    {
        var user = await _userManager.FindByNameAsync(username);

        if (user == null || user.IsDeleted)
        {
            return Result.Failure<AuthResponse>("Invalid credentials");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            if (lockoutEnd.HasValue)
            {
                var remaining = lockoutEnd.Value - DateTimeOffset.UtcNow;
                var minutes = (int)Math.Ceiling(Math.Max(remaining.TotalMinutes, 0));
                return Result.Failure<AuthResponse>($"Account locked due to too many failed attempts. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}.");
            }

            return Result.Failure<AuthResponse>("Account locked due to too many failed attempts. Try again later.");
        }

        if (!result.Succeeded)
        {
            var maxAttempts = _userManager.Options.Lockout.MaxFailedAccessAttempts;
            var failedCount = await _userManager.GetAccessFailedCountAsync(user);
            var attemptsLeft = Math.Max(0, maxAttempts - failedCount);
            return Result.Failure<AuthResponse>($"Invalid credentials. {attemptsLeft} attempt{(attemptsLeft == 1 ? "" : "s")} remaining before lockout.");
        }

        var token = await GenerateJwtToken(user);

        return Result.Success(new AuthResponse(token, user.Id, user.UserName!, user.Email));
    }

    /// <summary>
    /// Signs the current user out by clearing the Identity session.
    /// </summary>
    public async Task<Result> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return Result.Success();
    }

    /// <summary>
    /// Changes a user's password after verifying the current password.
    /// </summary>
    public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.IsDeleted)
            return Result.Failure("User not found.", ErrorType.NotFound);

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(errors);
        }

        return Result.Success();
    }

    /// <summary>
    /// Generates a JWT for the specified user.
    /// </summary>
    private async Task<string> GenerateJwtToken(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGeneration123!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "ForumApi",
            audience: _configuration["Jwt:Audience"] ?? "ForumClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```
## File: \src\Forum.Infrastructure\DependencyInjection.cs
```cs
using Forum.Application.Common.Interfaces;
using Forum.Application.Repositories;
using Forum.Infrastructure.Data;
using Forum.Infrastructure.Repositories;
using Forum.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Forum.Infrastructure;

/// <summary>
/// Provides extension methods for registering all Infrastructure layer services
/// into the dependency injection container.
/// Includes database context, repositories, Unit of Work, and the authentication service.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure layer services with the dependency injection container.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Data Source=forum.db";
        
        services.AddDbContext<ForumDbContext>(options =>
            options.UseSqlite(connectionString));

        // Register repository implementations
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IThreadRepository, ThreadRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVoteRepository, VoteRepository>();

        // Register the Unit of Work for coordinating transactional saves across repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register the authentication service
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}```
