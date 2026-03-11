## File: \src\Forum.Api\Endpoints\VoteEndpoints.cs
``cs
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
                var userId = httpContext.User.GetUserId();
                if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

                var result = await mediator.Send(new CastVoteCommand(userId, commentId, dto.Value), ct);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : result.ToProblemDetails();
            })
            .RequireAuthorization()
            .WithTags("Votes");

        return app;
    }
}
``

## File: \src\Forum.Application\DTOs\Vote\CastVoteDto.cs
``cs
namespace Forum.Application.DTOs.Vote;

/// <summary>
/// Payload for casting or toggling a vote on a comment.
/// </summary>
/// <param name="Value">Vote direction: 1 (upvote) or -1 (downvote).</param>
public record CastVoteDto(int Value);
``

## File: \src\Forum.Application\DTOs\Vote\VoteResponseDto.cs
``cs
namespace Forum.Application.DTOs.Vote;

/// <summary>
/// Returned after a vote is cast, containing the updated score and the user's current vote state.
/// </summary>
public record VoteResponseDto(int CommentId, int NewScore, int UserVote);
``

## File: \src\Forum.Application\Features\Votes\Commands\CastVote.cs
``cs
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
``

## File: \src\Forum.Application\Repositories\IVoteRepository.cs
``cs
using Forum.Domain.Entities;

namespace Forum.Application.Repositories;

/// <summary>
/// Repository for managing comment votes (upvotes/downvotes).
/// </summary>
public interface IVoteRepository : IRepository<Vote>
{
    Task<Vote?> GetByUserAndCommentAsync(string userId, int commentId, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetScoresForCommentsAsync(IEnumerable<int> commentIds, CancellationToken ct = default);
    Task<Dictionary<int, int>> GetUserVotesForCommentsAsync(string userId, IEnumerable<int> commentIds, CancellationToken ct = default);
}
``

## File: \src\Forum.Blazor\Components\App.razor
``razor
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

``

## File: \src\Forum.Blazor\Components\Routes.razor
``razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)"/>
        <FocusOnNavigate RouteData="routeData" Selector="h1"/>
    </Found>
</Router>

``

## File: \src\Forum.Blazor\Components\_Imports.razor
``razor
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
``

## File: \src\Forum.Blazor\Components\Layout\MainLayout.razor
``razor
@inherits LayoutComponentBase

<div class="site-wrapper">
    <NavMenu />

    <main class="main-body">
        @Body
    </main>

    @if (!IsHomePage)
    {
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
    private HttpContext? HttpContext { get; set; }

    private bool IsHomePage => NavigationManager.Uri.TrimEnd('/') == NavigationManager.BaseUri.TrimEnd('/');

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
}
``

## File: \src\Forum.Blazor\Components\Layout\NavMenu.razor
``razor
@using Forum.Blazor.Services
@inject CategoryService CategoryService
@inject IAuthClientService AuthService
@inject NavigationManager Navigation

<header class="main-header">
    <div class="top-row-black">
        <div class="header-container">
            <div class="brand">
                <a href="/">OVER<span class="red-text">TIME</span></a>
            </div>
            <span class="brand-tagline">Debates on current affairs and recent past of your favourite sports.</span>
            <div class="user-actions desktop-only">
                <AuthorizeView>
                    <Authorized>
                        <a href="/profile" class="user-greeting">
                            @context.User.Identity?.Name
                        </a>
                        <AuthorizeView Roles="Admin">
                            <Authorized Context="adminContext">
                                <a href="/admin" class="btn-outline">Admin</a>
                            </Authorized>
                        </AuthorizeView>
                        <button class="btn-outline" @onclick="HandleLogout">Log Out</button>
                    </Authorized>
                    <NotAuthorized>
                        <a href="/login" class="btn-outline">Log In</a>
                        <a href="/register" class="btn-red">Register</a>
                    </NotAuthorized>
                </AuthorizeView>
            </div>
            <button class="mobile-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navMenuCollapse" aria-controls="navMenuCollapse" aria-expanded="false" aria-label="Toggle navigation">
                <span></span>
                <span></span>
                <span></span>
            </button>
        </div>
    </div>

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
    private List<CategoryDto>? _categories;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _categories = await CategoryService.GetAllAsync();
        }
        catch
        {
            _categories = [];
        }
    }

    private async Task HandleLogout()
    {
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("/", forceLoad: true);
    }
}
``

## File: \src\Forum.Blazor\Components\Layout\NavMenu.razor.css
``css
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
.navbar-toggler:checked {
    background-color: rgba(255, 255, 255, 0.5);
}
.top-row {
    height: 3.5rem;
    background-color: rgba(0,0,0,0.4);
}
.navbar-brand {
    font-size: 1.1rem;
}
.bi {
    display: inline-block;
    position: relative;
    width: 1.25rem;
    height: 1.25rem;
    margin-right: 0.75rem;
    top: -1px;
    background-size: cover;
}
.bi-house-door-fill-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-house-door-fill' viewBox='0 0 16 16'%3E%3Cpath d='M6.5 14.5v-3.505c0-.245.25-.495.5-.495h2c.25 0 .5.25.5.5v3.5a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5v-7a.5.5 0 0 0-.146-.354L13 5.793V2.5a.5.5 0 0 0-.5-.5h-1a.5.5 0 0 0-.5.5v1.293L8.354 1.146a.5.5 0 0 0-.708 0l-6 6A.5.5 0 0 0 1.5 7.5v7a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5Z'/%3E%3C/svg%3E");
}
.bi-plus-square-fill-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-plus-square-fill' viewBox='0 0 16 16'%3E%3Cpath d='M2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2zm6.5 4.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3a.5.5 0 0 1 1 0z'/%3E%3C/svg%3E");
}
.bi-list-nested-nav-menu {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-list-nested' viewBox='0 0 16 16'%3E%3Cpath fill-rule='evenodd' d='M4.5 11.5A.5.5 0 0 1 5 11h10a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 3 7h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 1 3h10a.5.5 0 0 1 0 1H1a.5.5 0 0 1-.5-.5z'/%3E%3C/svg%3E");
}
.nav-item {
    font-size: 0.9rem;
    padding-bottom: 0.5rem;
}
    .nav-item:first-of-type {
        padding-top: 1rem;
    }
    .nav-item:last-of-type {
        padding-bottom: 1rem;
    }
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
.nav-item ::deep a.active {
    background-color: rgba(255,255,255,0.37);
    color: white;
}
.nav-item ::deep .nav-link:hover {
    background-color: rgba(255,255,255,0.1);
    color: white;
}
.nav-scrollable {
    display: none;
}
.navbar-toggler:checked ~ .nav-scrollable {
    display: block;
}
@media (min-width: 641px) {
    .navbar-toggler {
        display: none;
    }
    .nav-scrollable {
        display: block;
        height: calc(100vh - 3.5rem);
        overflow-y: auto;
    }
}

``

## File: \src\Forum.Blazor\Components\Pages\Admin.razor
``razor
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

    <div class="panel-tabs">
        <button class="panel-tab @(activeTab == "categories" ? "active" : "")" @onclick='() => activeTab = "categories"'>Categories</button>
        <button class="panel-tab @(activeTab == "threads" ? "active" : "")" @onclick='() => activeTab = "threads"'>Threads</button>
        <button class="panel-tab @(activeTab == "users" ? "active" : "")" @onclick='() => activeTab = "users"'>Users</button>
    </div>

    @if (!string.IsNullOrEmpty(statusMessage))
    {
        <div class="panel-status @(isError ? "error" : "success")">@statusMessage</div>
    }

    @* ===== CATEGORIES TAB ===== *@
    @if (activeTab == "categories")
    {
        <div class="panel-section panel-section-narrow">
            <h2>Manage Categories</h2>

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
                <label class="me-2 mb-0 panel-page-size-label">Threads per page:</label>
                <select class="form-select form-select-sm w-auto" value="@threadsPageSize" @onchange="OnThreadsPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_threadsSortByString" @onchange="OnThreadsSortChanged">
                    <option value="@ThreadSortBy.Newest.ToString()">Newest</option>
                    <option value="@ThreadSortBy.Oldest.ToString()">Oldest</option>
                    <option value="@ThreadSortBy.Category.ToString()">Category</option>
                    <option value="@ThreadSortBy.Author.ToString()">Author</option>
                </select>

                <div class="ms-auto">
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
                                    ttShow.Add(1);
                                    ttShow.Add(ttPages);
                                    if (threadsPage > 1) ttShow.Add(threadsPage - 1);
                                    ttShow.Add(threadsPage);
                                    if (threadsPage < ttPages) ttShow.Add(threadsPage + 1);

                                    int? ttLast = null;
                                    foreach (var pageNum in ttShow)
                                    {
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
                <label class="me-2 mb-0 panel-page-size-label">Users per page:</label>
                <select class="form-select form-select-sm w-auto" value="@usersPageSize" @onchange="OnUsersPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_usersSortByString" @onchange="OnUsersSortChanged">
                    <option value="@UserSortBy.Username.ToString()">Username</option>
                    <option value="@UserSortBy.Threads.ToString()">Threads</option>
                    <option value="@UserSortBy.Comments.ToString()">Comments</option>
                </select>

                <div class="ms-auto">
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
                                    uuShow.Add(1);
                                    uuShow.Add(uuPages);
                                    if (usersPage > 1) uuShow.Add(usersPage - 1);
                                    uuShow.Add(usersPage);
                                    if (usersPage < uuPages) uuShow.Add(usersPage + 1);

                                    int? uuLast = null;
                                    foreach (var pageNum in uuShow)
                                    {
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
                                    @if (!user.IsDeleted)
                                    {
                                        @if (editingUserId == user.Id)
                                        {
                                            <button class="btn-success-sm" @onclick="() => SaveUserName(user.Id)">Save</button>
                                            <button class="btn-outline-sm" @onclick="CancelEditUser">Cancel</button>
                                        }
                                        else
                                        {
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
    private string activeTab = "categories";
    private string statusMessage = "";
    private bool isError;
    private bool isLoading;

    // Categories
    private List<CategoryDto> categories = [];
    private string newCategoryName = "";

    // Threads
    private List<ThreadSummaryDto> threads = [];
    private PagedResult<ThreadSummaryDto>? threadsPaged;
    private int threadsPage = 1;
    private int threadsPageSize = 5;
    private ThreadSortBy _threadsSortBy = ThreadSortBy.Newest;
    private string _threadsSortByString => _threadsSortBy.ToString();

    // Users
    private List<UserDto> users = [];
    private PagedResult<UserDto>? usersPaged;
    private int usersPage = 1;
    private int usersPageSize = 5;
    private UserSortBy _usersSortBy = UserSortBy.Username;
    private string _usersSortByString => _usersSortBy.ToString();
    private string? editingUserId;
    private string editingUserName = "";

    private bool _loaded;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
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
            newCategoryName = "";
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
        if (parsed == _threadsSortBy) return;
        _threadsSortBy = parsed;
        threadsPage = 1;
        await LoadThreads();
    }

    private async Task DeleteThread(int id, string title)
    {
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
        if (newSize == threadsPageSize) return;
        threadsPageSize = newSize;
        threadsPage = 1;
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
            SetStatus("Failed to load users. You may not have admin permissions.", true);
        }
    }

    private async Task OnUsersSortChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!Enum.TryParse<UserSortBy>(e.Value.ToString(), out var parsed)) return;
        if (parsed == _usersSortBy) return;
        _usersSortBy = parsed;
        usersPage = 1;
        await LoadUsers();
    }

    private async Task OnUsersPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == usersPageSize) return;
        usersPageSize = newSize;
        usersPage = 1;
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
        editingUserId = id;
        editingUserName = currentName;
    }

    private void CancelEditUser()
    {
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

}
``

## File: \src\Forum.Blazor\Components\Pages\CategoryThreads.razor
``razor
@page "/category/{CategoryName}"
@using Forum.Application.DTOs.Thread
@inject CategoryService CategoryService
@inject ThreadService ThreadService

<PageTitle>@CategoryName - Sport Forum</PageTitle>

<link rel="stylesheet" href="/css/category.css" />
<link rel="stylesheet" href="/css/forum-table.css" />

<div class="category-page">
    <div class="category-hero @(CategoryName?.ToLower().Replace(" ", ""))">
        <div class="hero-content">
            <h1 class="category-title">@CategoryName?.ToUpper()</h1>
        </div>
    </div>

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
                <label class="me-2 mb-0" style="color: #8b949e;">Threads per page:</label>
                <select class="form-select form-select-sm w-auto category-pagination-select" value="@_pageSize" @onchange="OnPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                <label class="ms-3 me-2 mb-0" style="color: #8b949e;">Sort:</label>
                <select class="form-select form-select-sm w-auto category-sort-select" value="@_sortByString" @onchange="OnSortChanged">
                    <option value="@ThreadSortBy.Newest.ToString()">Newest</option>
                    <option value="@ThreadSortBy.Oldest.ToString()">Oldest</option>
                    <option value="@ThreadSortBy.MostComments.ToString()">Most comments</option>
                    <option value="@ThreadSortBy.RecentlyUpdated.ToString()">Last commented</option>
                </select>

                <div class="ms-auto">
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
                                    pagesToShow.Add(1);
                                    pagesToShow.Add(totalPages);
                                    if (_pageNumber > 1) pagesToShow.Add(_pageNumber - 1);
                                    pagesToShow.Add(_pageNumber);
                                    if (_pageNumber < totalPages) pagesToShow.Add(_pageNumber + 1);

                                    int? lastShown = null;
                                    foreach (var pageNum in pagesToShow)
                                    {
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
    public string CategoryName { get; set; } = string.Empty;

    private PagedResult<ThreadSummaryDto>? _threadsPage;
    private int _pageNumber = 1;
    private int _pageSize = 5;
    private int? _categoryId;

    private ThreadSortBy _sortBy = ThreadSortBy.Newest;
    private string _sortByString => _sortBy.ToString();

    protected override async Task OnParametersSetAsync()
    {
        var categories = await CategoryService.GetAllAsync();
        var category = categories?.FirstOrDefault(c =>
            string.Equals(c.Name, CategoryName, StringComparison.OrdinalIgnoreCase));

        _categoryId = category?.CategoryId;
        _pageNumber = 1;
        await LoadThreads();
    }

    private async Task LoadThreads(int? page = null)
    {
        var pageToLoad = page ?? _pageNumber;

        if (_categoryId == null)
        {
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
        _threadsPage = result ?? PagedResult<ThreadSummaryDto>.Create([], 0, pageToLoad, _pageSize);
        _pageNumber = _threadsPage.PageNumber;
        StateHasChanged();
    }

    private async Task OnPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == _pageSize) return;

        _pageSize = newSize;
        _pageNumber = 1;
        await LoadThreads(_pageNumber);
    }

    private async Task OnSortChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        var val = e.Value.ToString();
        if (Enum.TryParse<ThreadSortBy>(val, out var parsed))
        {
            if (parsed == _sortBy) return;
            _sortBy = parsed;
            _pageNumber = 1;
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

    private static string FormatDate(DateTime dt) => dt.ToString("MMM dd, yyyy");
    private static string FormatTime(DateTime dt) => dt.ToString("HH:mm");

    private static DateTime GetLastActivityDate(ThreadSummaryDto t)
    {
        if (t.LastCommentTime.HasValue)
            return t.LastCommentTime.Value;
        return t.TimeUpdated != default ? t.TimeUpdated : t.TimeCreated;
    }
}
``

## File: \src\Forum.Blazor\Components\Pages\CreateThread.razor
``razor
@page "/create-thread"
@page "/create-thread/{CategoryName}"
@inject CategoryService CategoryService
@inject ThreadService ThreadService
@inject NavigationManager NavigationManager

<PageTitle>Create New Thread</PageTitle>

<link rel="stylesheet" href="/css/create-thread.css" />

<AuthorizeView>
<NotAuthorized>
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

    @if (_isSubmitting)
    {
        <div class="submitting">creating thread...</div>
    }
    else
    {
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
                <button type="button" class="btn btn-secondary" @onclick="@(() => NavigationManager.NavigateTo("/"))">
                    cancel
                </button>
            </div>
        </EditForm>
    }

    @if (!string.IsNullOrEmpty(_errorMessage))
    {
        <div class="error-message">@_errorMessage</div>
    }
</div>

</Authorized>
</AuthorizeView>

@code {
    [Parameter]
    public string? CategoryName { get; set; }

    private CreateThreadModel _newThread = new();
    private List<CategoryDto>? _categories;
    private bool _isSubmitting;
    private string _errorMessage = string.Empty;

    // model with validation attributes
    public class CreateThreadModel
    {
        [Required(ErrorMessage = "title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "title must be between 3 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "category is required")]
        public string CategoryIdString { get; set; } = string.Empty;

        public int CategoryId => string.IsNullOrEmpty(CategoryIdString) ? 0 : int.Parse(CategoryIdString);

        [Required(ErrorMessage = "message is required")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "message must be between 10 and 2000 characters")]
        public string Body { get; set; } = string.Empty;

        public CreateThreadDto ToDto() => new(Title, CategoryId, Body);
    }

    protected override async Task OnInitializedAsync()
    {
        var categories = await CategoryService.GetAllAsync();
        _categories = categories?.ToList();

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
                // navigate to threads page on success
                var selectedCategory = _categories.FirstOrDefault(c => c.CategoryId == _newThread.CategoryId);
                if (selectedCategory != null)
                {
                    NavigationManager.NavigateTo($"/category/{selectedCategory.Name}");
                }
                else
                {
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
            _errorMessage = "an error occurred while creating the thread.";
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
``

## File: \src\Forum.Blazor\Components\Pages\Error.razor
``razor
@page "/Error"
@using System.Diagnostics
<PageTitle>Error</PageTitle>
<h1 class="text-danger">Error.</h1>
<h2 class="text-danger">An error occurred while processing your request.</h2>
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
    [CascadingParameter] private HttpContext? HttpContext { get; set; }
    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    protected override void OnInitialized() =>
        RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;
}

``

## File: \src\Forum.Blazor\Components\Pages\Home.razor
``razor
@page "/"
@inject CategoryService CategoryService
@inject ThreadService ThreadService

<PageTitle>Overtime</PageTitle>

<link rel="stylesheet" href="/css/home.css" />

<div class="home-wrapper">
    <div class="home-overlay">

        <div class="home-content">

            @if (_popularThreads == null)
            {
                <div class="loading">loading...</div>
            }
            else
            {
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
    private List<PopularThreadItem>? _popularThreads;
    private List<CategoryWithThreads>? _popularCategories;

    private class CategoryWithThreads
    {
        public string Name { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int ThreadCount { get; set; }
        public DateTime LatestActivity { get; set; }
        public List<ThreadSummaryDto> RecentThreads { get; set; } = [];
    }

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
}
``

## File: \src\Forum.Blazor\Components\Pages\Login.razor
``razor
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

                <button type="submit" class="btn btn-primary" disabled="@isLoading">
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
    private bool isLoading = false;

    private async Task HandleLogin()
    {
        errorMessage = string.Empty;

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
                Navigation.NavigateTo("/");
            }
            else
            {

                errorMessage = result.ErrorMessage ?? "Invalid username or password.";
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

}
``

## File: \src\Forum.Blazor\Components\Pages\Logout.razor
``razor
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
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("/");
    }
}
``

## File: \src\Forum.Blazor\Components\Pages\Profile.razor
``razor
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

    <div class="panel-tabs">
        <button class="panel-tab @(activeTab == "account" ? "active" : "")" @onclick='() => activeTab = "account"'>Account Settings</button>
        <button class="panel-tab @(activeTab == "mythreads" ? "active" : "")" @onclick="ActivateMyThreads">My Threads</button>
        <button class="panel-tab @(activeTab == "mycomments" ? "active" : "")" @onclick="ActivateMyComments">My Comments</button>
    </div>

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
            @if (isLoadingProfile)
            {
                <div class="text-center">
                    <div class="spinner-border"></div>
                </div>
            }
            else
            {
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
                <label class="me-2 mb-0" style="color: #8b949e;">Threads per page:</label>
                <select class="form-select form-select-sm w-auto panel-pagination-select" value="@myThreadsPageSize" @onchange="OnMyThreadsPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_myThreadsSortByString" @onchange="OnMyThreadsSortChanged">
                    <option value="@ThreadSortBy.Newest.ToString()">Newest</option>
                    <option value="@ThreadSortBy.Oldest.ToString()">Oldest</option>
                    <option value="@ThreadSortBy.Category.ToString()">Category</option>
                </select>

                <div class="ms-auto">
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
                                    ttShow.Add(1);
                                    ttShow.Add(ttPages);
                                    if (myThreadsPage > 1) ttShow.Add(myThreadsPage - 1);
                                    ttShow.Add(myThreadsPage);
                                    if (myThreadsPage < ttPages) ttShow.Add(myThreadsPage + 1);

                                    int? ttLast = null;
                                    foreach (var pageNum in ttShow)
                                    {
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
                <label class="me-2 mb-0" style="color: #8b949e;">Comments per page:</label>
                <select class="form-select form-select-sm w-auto panel-pagination-select" value="@myCommentsPageSize" @onchange="OnMyCommentsPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                <label class="ms-3 me-2 mb-0 panel-page-size-label">Sort:</label>
                <select class="form-select form-select-sm w-auto" value="@_myCommentsSortByString" @onchange="OnMyCommentsSortChanged">
                    <option value="@CommentSortBy.Newest.ToString()">Newest</option>
                    <option value="@CommentSortBy.Oldest.ToString()">Oldest</option>
                </select>

                <div class="ms-auto">
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
                                    ccShow.Add(1);
                                    ccShow.Add(ccPages);
                                    if (myCommentsPage > 1) ccShow.Add(myCommentsPage - 1);
                                    ccShow.Add(myCommentsPage);
                                    if (myCommentsPage < ccPages) ccShow.Add(myCommentsPage + 1);

                                    int? ccLast = null;
                                    foreach (var pageNum in ccShow)
                                    {
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
    private string activeTab = "account";

    private ProfileModel profileModel = new();
    private UpdateEmailModel emailModel = new();
    private ChangePasswordModel passwordModel = new();

    private string errorMessage = string.Empty;
    private string successMessage = string.Empty;

    private bool isLoading = false;
    private bool isLoadingProfile = true;

    private string? userId;

    // My Threads
    private List<ThreadSummaryDto> myThreads = [];
    private PagedResult<ThreadSummaryDto>? myThreadsPaged;
    private int myThreadsPage = 1;
    private int myThreadsPageSize = 5;
    private bool myThreadsLoaded;
    private ThreadSortBy _myThreadsSortBy = ThreadSortBy.Newest;
    private string _myThreadsSortByString => _myThreadsSortBy.ToString();

    // My Comments
    private List<CommentDto> myComments = [];
    private PagedResult<CommentDto>? myCommentsPaged;
    private int myCommentsPage = 1;
    private int myCommentsPageSize = 5;
    private bool myCommentsLoaded;
    private CommentSortBy _myCommentsSortBy = CommentSortBy.Newest;
    private string _myCommentsSortByString => _myCommentsSortBy.ToString();

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        userId = authState.User.FindFirstValue(ClaimTypes.NameIdentifier);

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
                emailModel.NewEmail = profile.Email ?? string.Empty;
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
                profileModel.Email = emailModel.NewEmail;
            }
            else
            {
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
                passwordModel = new ChangePasswordModel();
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
        if (parsed == _myThreadsSortBy) return;
        _myThreadsSortBy = parsed;
        myThreadsPage = 1;
        await LoadMyThreads();
    }

    private async Task OnMyThreadsPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == myThreadsPageSize) return;
        myThreadsPageSize = newSize;
        myThreadsPage = 1;
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
        if (parsed == _myCommentsSortBy) return;
        _myCommentsSortBy = parsed;
        myCommentsPage = 1;
        await LoadMyComments();
    }

    private async Task OnMyCommentsPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == myCommentsPageSize) return;
        myCommentsPageSize = newSize;
        myCommentsPage = 1;
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
}
``

## File: \src\Forum.Blazor\Components\Pages\Register.razor
``razor
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

            @if (!string.IsNullOrEmpty(errorMessage))
            {
                <div class="alert alert-danger">
                    @errorMessage
                </div>
            }

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

    private CancellationTokenSource _cts = new();
    private RegisterRequest registerModel = new();
    private string errorMessage = string.Empty;
    private bool isLoading = false;
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

}
``

## File: \src\Forum.Blazor\Components\Pages\ThreadDetails.razor
``razor
@page "/thread/{Id:int}"
@inject ThreadService ThreadService
@inject CommentService CommentService
@inject IJSRuntime JS

<link rel="stylesheet" href="/css/thread-details.css" />

<PageTitle>Thread Details - Overtime</PageTitle>

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

        @if (_thread.BodyComment != null && !string.IsNullOrEmpty(_thread.BodyComment.Content))
        {
            <div class="body-comment">
                <div class="comment-content">@_thread.BodyComment.Content</div>
            </div>
        }

        <div class="comments-section">
            <h3>comments (@(_commentsPage?.TotalCount ?? 0))</h3>

            <div class="d-flex align-items-center mb-2">
                <label class="me-2 mb-0">Comments per page:</label>
                <select class="form-select form-select-sm w-auto" value="@_pageSize" @onchange="OnPageSizeChanged">
                    @foreach (var size in new[] { 5, 10, 15, 20 })
                    {
                        <option value="@size">@size</option>
                    }
                </select>

                <div class="ms-auto">
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
                            @if (!string.IsNullOrEmpty(comment.ParentCommentAuthorUserName))
                            {
                                <span class="reply-info">replying to @comment.ParentCommentAuthorUserName</span>
                            }
                        </div>
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
                                    <div class="vote-bar">
                                        <button class="vote-btn upvote @(GetUserVote(comment.CommentId) == 1 ? "active-up" : "")"
                                                @onclick="async () => await Vote(comment.CommentId, 1)"
                                                title="Upvote">â–²</button>
                                        <span class="vote-score @(GetScore(comment.CommentId) > 0 ? "score-positive" : GetScore(comment.CommentId) < 0 ? "score-negative" : "score-zero")">
                                            @GetScore(comment.CommentId)
                                        </span>
                                        <button class="vote-btn downvote @(GetUserVote(comment.CommentId) == -1 ? "active-down" : "")"
                                                @onclick="async () => await Vote(comment.CommentId, -1)"
                                                title="Downvote">â–¼</button>
                                    </div>
                                </Authorized>
                                <NotAuthorized>
                                    <div class="vote-bar vote-bar-locked" title="Log in to vote">
                                        <button class="vote-btn upvote locked" disabled>â–²</button>
                                        <span class="vote-score score-zero">@GetScore(comment.CommentId)</span>
                                        <button class="vote-btn downvote locked" disabled>â–¼</button>
                                    </div>
                                </NotAuthorized>
                            </AuthorizeView>

                            <AuthorizeView>
                                <Authorized Context="replyAuth">
                                    @if (_replyingToCommentId != comment.CommentId)
                                    {
                                        <button class="btn btn-link btn-sm p-0" @onclick="() => StartReply(comment.CommentId)">Reply</button>
                                    }
                                </Authorized>
                            </AuthorizeView>

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
    public int Id { get; set; }

    private ThreadDetailDto? _thread;
    private bool _isLoading = true;

    private PagedResult<CommentDto>? _commentsPage;
    private int _pageNumber = 1;
    private int _pageSize = 10;

    private string _newCommentContent = string.Empty;
    private string _commentError = string.Empty;
    private bool _isSubmitting;
    private int? _replyingToCommentId;
    private string _replyContent = string.Empty;

    // Local vote state â€” kept in sync after each vote API call to avoid full reload
    private Dictionary<int, int> _voteScores = [];
    private Dictionary<int, int> _userVotes = [];

    protected override async Task OnParametersSetAsync()
    {
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
                PopulateVoteData();
            }
        }
        catch
        {
            _commentsPage = null;
        }
        StateHasChanged();
    }

    // Seed local vote caches from the server response
    private void PopulateVoteData()
    {
        if (_commentsPage?.Items == null) return;
        foreach (var c in _commentsPage.Items)
        {
            _voteScores[c.CommentId] = c.VoteScore;
            _userVotes[c.CommentId] = c.CurrentUserVote ?? 0;
        }
    }

    private int GetScore(int commentId) =>
        _voteScores.TryGetValue(commentId, out var s) ? s : 0;

    private int GetUserVote(int commentId) =>
        _userVotes.TryGetValue(commentId, out var v) ? v : 0;

    private async Task Vote(int commentId, int direction)
    {
        var result = await CommentService.CastVoteAsync(commentId, direction);
        if (result != null)
        {
            _voteScores[commentId] = result.NewScore;
            _userVotes[commentId] = result.UserVote;
        }
    }

    private async Task OnPageSizeChanged(ChangeEventArgs e)
    {
        if (e?.Value == null) return;
        if (!int.TryParse(e.Value.ToString(), out var newSize)) return;
        if (newSize == _pageSize) return;
        _pageSize = newSize;
        _pageNumber = 1;
        await LoadComments(_pageNumber);
    }

    private void StartReply(int commentId)
    {
        _replyingToCommentId = commentId;
        _replyContent = string.Empty;
    }

    private void CancelReply()
    {
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
                _replyingToCommentId = null;
                _replyContent = string.Empty;
                await LoadComments(_pageNumber);
            }
        }
        catch { }
        finally { _isSubmitting = false; }
    }

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
}
``

## File: \src\Forum.Blazor\Components\Pages\Threads.razor
``razor
@page "/threads"
@inject HttpClient Http

<link rel="stylesheet" href="/css/forum-table.css" />

<div class="forum-view-wrapper">
    @* breadcrumb navigation with links *@
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
    [Parameter] public string? CategoryName { get; set; }
    private PagedResult<ThreadDto>? threads;

    protected override async Task OnParametersSetAsync() {
        await LoadThreads(); // load threads whenever parameter changes
    }

    private async Task LoadThreads() {
        var url = "api/threads?pageSize=20";
        if (!string.IsNullOrEmpty(CategoryName)) url += $"&CategoryName={Uri.EscapeDataString(CategoryName)}";
        try {
            var response = await Http.GetFromJsonAsync<PagedResult<ThreadSummaryDto>>(url); // get data from api
            if (response != null)
            {
                // map ThreadSummaryDto to ThreadDto with requested property names
                threads = PagedResult<ThreadDto>.Create(
                    response.Items.Select(t => new ThreadDto(
                        t.ThreadId,
                        t.AuthorUserName,
                        t.TimeCreated,
                        t.Title,
                        "", // body not needed in listing
                        t.CommentCount
                    )).ToList(),
                    response.TotalCount,
                    response.PageNumber,
                    response.PageSize
                );
            }
        } catch {
            threads = PagedResult<ThreadDto>.Create(new List<ThreadDto>(), 0, 1, 20);
        }
    }

    // thread dto with requested property names
    private record ThreadDto(
        int Id,
        string Author,
        DateTime Created,
        string Title,
        string Body,
        int CommentsCount
    );
}
``

## File: \src\Forum.Blazor\obj\Debug\net8.0\scopedcss\bundle\Forum.Blazor.styles.css
``css
/* _content/Forum.Blazor/Components/Layout/NavMenu.razor.rz.scp.css */
.navbar-toggler[b-zuzwusafni] {
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
.navbar-toggler:checked[b-zuzwusafni] {
    background-color: rgba(255, 255, 255, 0.5);
}
.top-row[b-zuzwusafni] {
    height: 3.5rem;
    background-color: rgba(0,0,0,0.4);
}
.navbar-brand[b-zuzwusafni] {
    font-size: 1.1rem;
}
.bi[b-zuzwusafni] {
    display: inline-block;
    position: relative;
    width: 1.25rem;
    height: 1.25rem;
    margin-right: 0.75rem;
    top: -1px;
    background-size: cover;
}
.bi-house-door-fill-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-house-door-fill' viewBox='0 0 16 16'%3E%3Cpath d='M6.5 14.5v-3.505c0-.245.25-.495.5-.495h2c.25 0 .5.25.5.5v3.5a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5v-7a.5.5 0 0 0-.146-.354L13 5.793V2.5a.5.5 0 0 0-.5-.5h-1a.5.5 0 0 0-.5.5v1.293L8.354 1.146a.5.5 0 0 0-.708 0l-6 6A.5.5 0 0 0 1.5 7.5v7a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5Z'/%3E%3C/svg%3E");
}
.bi-plus-square-fill-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-plus-square-fill' viewBox='0 0 16 16'%3E%3Cpath d='M2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2zm6.5 4.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3a.5.5 0 0 1 1 0z'/%3E%3C/svg%3E");
}
.bi-list-nested-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-list-nested' viewBox='0 0 16 16'%3E%3Cpath fill-rule='evenodd' d='M4.5 11.5A.5.5 0 0 1 5 11h10a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 3 7h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 1 3h10a.5.5 0 0 1 0 1H1a.5.5 0 0 1-.5-.5z'/%3E%3C/svg%3E");
}
.nav-item[b-zuzwusafni] {
    font-size: 0.9rem;
    padding-bottom: 0.5rem;
}
    .nav-item:first-of-type[b-zuzwusafni] {
        padding-top: 1rem;
    }
    .nav-item:last-of-type[b-zuzwusafni] {
        padding-bottom: 1rem;
    }
    .nav-item[b-zuzwusafni]  .nav-link {
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
.nav-item[b-zuzwusafni]  a.active {
    background-color: rgba(255,255,255,0.37);
    color: white;
}
.nav-item[b-zuzwusafni]  .nav-link:hover {
    background-color: rgba(255,255,255,0.1);
    color: white;
}
.nav-scrollable[b-zuzwusafni] {
    display: none;
}
.navbar-toggler:checked ~ .nav-scrollable[b-zuzwusafni] {
    display: block;
}
@media (min-width: 641px) {
    .navbar-toggler[b-zuzwusafni] {
        display: none;
    }
    .nav-scrollable[b-zuzwusafni] {
        display: block;
        height: calc(100vh - 3.5rem);
        overflow-y: auto;
    }
}

``

## File: \src\Forum.Blazor\obj\Debug\net8.0\scopedcss\Components\Layout\NavMenu.razor.rz.scp.css
``css
.navbar-toggler[b-zuzwusafni] {
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
.navbar-toggler:checked[b-zuzwusafni] {
    background-color: rgba(255, 255, 255, 0.5);
}
.top-row[b-zuzwusafni] {
    height: 3.5rem;
    background-color: rgba(0,0,0,0.4);
}
.navbar-brand[b-zuzwusafni] {
    font-size: 1.1rem;
}
.bi[b-zuzwusafni] {
    display: inline-block;
    position: relative;
    width: 1.25rem;
    height: 1.25rem;
    margin-right: 0.75rem;
    top: -1px;
    background-size: cover;
}
.bi-house-door-fill-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-house-door-fill' viewBox='0 0 16 16'%3E%3Cpath d='M6.5 14.5v-3.505c0-.245.25-.495.5-.495h2c.25 0 .5.25.5.5v3.5a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5v-7a.5.5 0 0 0-.146-.354L13 5.793V2.5a.5.5 0 0 0-.5-.5h-1a.5.5 0 0 0-.5.5v1.293L8.354 1.146a.5.5 0 0 0-.708 0l-6 6A.5.5 0 0 0 1.5 7.5v7a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5Z'/%3E%3C/svg%3E");
}
.bi-plus-square-fill-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-plus-square-fill' viewBox='0 0 16 16'%3E%3Cpath d='M2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2zm6.5 4.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3a.5.5 0 0 1 1 0z'/%3E%3C/svg%3E");
}
.bi-list-nested-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-list-nested' viewBox='0 0 16 16'%3E%3Cpath fill-rule='evenodd' d='M4.5 11.5A.5.5 0 0 1 5 11h10a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 3 7h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 1 3h10a.5.5 0 0 1 0 1H1a.5.5 0 0 1-.5-.5z'/%3E%3C/svg%3E");
}
.nav-item[b-zuzwusafni] {
    font-size: 0.9rem;
    padding-bottom: 0.5rem;
}
    .nav-item:first-of-type[b-zuzwusafni] {
        padding-top: 1rem;
    }
    .nav-item:last-of-type[b-zuzwusafni] {
        padding-bottom: 1rem;
    }
    .nav-item[b-zuzwusafni]  .nav-link {
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
.nav-item[b-zuzwusafni]  a.active {
    background-color: rgba(255,255,255,0.37);
    color: white;
}
.nav-item[b-zuzwusafni]  .nav-link:hover {
    background-color: rgba(255,255,255,0.1);
    color: white;
}
.nav-scrollable[b-zuzwusafni] {
    display: none;
}
.navbar-toggler:checked ~ .nav-scrollable[b-zuzwusafni] {
    display: block;
}
@media (min-width: 641px) {
    .navbar-toggler[b-zuzwusafni] {
        display: none;
    }
    .nav-scrollable[b-zuzwusafni] {
        display: block;
        height: calc(100vh - 3.5rem);
        overflow-y: auto;
    }
}

``

## File: \src\Forum.Blazor\obj\Debug\net8.0\scopedcss\projectbundle\Forum.Blazor.bundle.scp.css
``css
/* _content/Forum.Blazor/Components/Layout/NavMenu.razor.rz.scp.css */
.navbar-toggler[b-zuzwusafni] {
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
.navbar-toggler:checked[b-zuzwusafni] {
    background-color: rgba(255, 255, 255, 0.5);
}
.top-row[b-zuzwusafni] {
    height: 3.5rem;
    background-color: rgba(0,0,0,0.4);
}
.navbar-brand[b-zuzwusafni] {
    font-size: 1.1rem;
}
.bi[b-zuzwusafni] {
    display: inline-block;
    position: relative;
    width: 1.25rem;
    height: 1.25rem;
    margin-right: 0.75rem;
    top: -1px;
    background-size: cover;
}
.bi-house-door-fill-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-house-door-fill' viewBox='0 0 16 16'%3E%3Cpath d='M6.5 14.5v-3.505c0-.245.25-.495.5-.495h2c.25 0 .5.25.5.5v3.5a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5v-7a.5.5 0 0 0-.146-.354L13 5.793V2.5a.5.5 0 0 0-.5-.5h-1a.5.5 0 0 0-.5.5v1.293L8.354 1.146a.5.5 0 0 0-.708 0l-6 6A.5.5 0 0 0 1.5 7.5v7a.5.5 0 0 0 .5.5h4a.5.5 0 0 0 .5-.5Z'/%3E%3C/svg%3E");
}
.bi-plus-square-fill-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-plus-square-fill' viewBox='0 0 16 16'%3E%3Cpath d='M2 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2H2zm6.5 4.5v3h3a.5.5 0 0 1 0 1h-3v3a.5.5 0 0 1-1 0v-3h-3a.5.5 0 0 1 0-1h3v-3a.5.5 0 0 1 1 0z'/%3E%3C/svg%3E");
}
.bi-list-nested-nav-menu[b-zuzwusafni] {
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' fill='white' class='bi bi-list-nested' viewBox='0 0 16 16'%3E%3Cpath fill-rule='evenodd' d='M4.5 11.5A.5.5 0 0 1 5 11h10a.5.5 0 0 1 0 1H5a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 3 7h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5zm-2-4A.5.5 0 0 1 1 3h10a.5.5 0 0 1 0 1H1a.5.5 0 0 1-.5-.5z'/%3E%3C/svg%3E");
}
.nav-item[b-zuzwusafni] {
    font-size: 0.9rem;
    padding-bottom: 0.5rem;
}
    .nav-item:first-of-type[b-zuzwusafni] {
        padding-top: 1rem;
    }
    .nav-item:last-of-type[b-zuzwusafni] {
        padding-bottom: 1rem;
    }
    .nav-item[b-zuzwusafni]  .nav-link {
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
.nav-item[b-zuzwusafni]  a.active {
    background-color: rgba(255,255,255,0.37);
    color: white;
}
.nav-item[b-zuzwusafni]  .nav-link:hover {
    background-color: rgba(255,255,255,0.1);
    color: white;
}
.nav-scrollable[b-zuzwusafni] {
    display: none;
}
.navbar-toggler:checked ~ .nav-scrollable[b-zuzwusafni] {
    display: block;
}
@media (min-width: 641px) {
    .navbar-toggler[b-zuzwusafni] {
        display: none;
    }
    .nav-scrollable[b-zuzwusafni] {
        display: block;
        height: calc(100vh - 3.5rem);
        overflow-y: auto;
    }
}

``

## File: \src\Forum.Blazor\wwwroot\app.css
``css
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

``

## File: \src\Forum.Blazor\wwwroot\bootstrap\bootstrap.min.css
``css
@charset "UTF-8";:root{--bs-blue:#0d6efd;--bs-indigo:#6610f2;--bs-purple:#6f42c1;--bs-pink:#d63384;--bs-red:#dc3545;--bs-orange:#fd7e14;--bs-yellow:#ffc107;--bs-green:#198754;--bs-teal:#20c997;--bs-cyan:#0dcaf0;--bs-white:#fff;--bs-gray:#6c757d;--bs-gray-dark:#343a40;--bs-gray-100:#f8f9fa;--bs-gray-200:#e9ecef;--bs-gray-300:#dee2e6;--bs-gray-400:#ced4da;--bs-gray-500:#adb5bd;--bs-gray-600:#6c757d;--bs-gray-700:#495057;--bs-gray-800:#343a40;--bs-gray-900:#212529;--bs-primary:#0d6efd;--bs-secondary:#6c757d;--bs-success:#198754;--bs-info:#0dcaf0;--bs-warning:#ffc107;--bs-danger:#dc3545;--bs-light:#f8f9fa;--bs-dark:#212529;--bs-primary-rgb:13,110,253;--bs-secondary-rgb:108,117,125;--bs-success-rgb:25,135,84;--bs-info-rgb:13,202,240;--bs-warning-rgb:255,193,7;--bs-danger-rgb:220,53,69;--bs-light-rgb:248,249,250;--bs-dark-rgb:33,37,41;--bs-white-rgb:255,255,255;--bs-black-rgb:0,0,0;--bs-body-rgb:33,37,41;--bs-font-sans-serif:system-ui,-apple-system,"Segoe UI",Roboto,"Helvetica Neue",Arial,"Noto Sans","Liberation Sans",sans-serif,"Apple Color Emoji","Segoe UI Emoji","Segoe UI Symbol","Noto Color Emoji";--bs-font-monospace:SFMono-Regular,Menlo,Monaco,Consolas,"Liberation Mono","Courier New",monospace;--bs-gradient:linear-gradient(180deg, rgba(255, 255, 255, 0.15), rgba(255, 255, 255, 0));--bs-body-font-family:var(--bs-font-sans-serif);--bs-body-font-size:1rem;--bs-body-font-weight:400;--bs-body-line-height:1.5;--bs-body-color:#212529;--bs-body-bg:#fff}*,::after,::before{box-sizing:border-box}@media (prefers-reduced-motion:no-preference){:root{scroll-behavior:smooth}}body{margin:0;font-family:var(--bs-body-font-family);font-size:var(--bs-body-font-size);font-weight:var(--bs-body-font-weight);line-height:var(--bs-body-line-height);color:var(--bs-body-color);text-align:var(--bs-body-text-align);background-color:var(--bs-body-bg);-webkit-text-size-adjust:100%;-webkit-tap-highlight-color:transparent}hr{margin:1rem 0;color:inherit;background-color:currentColor;border:0;opacity:.25}hr:not([size]){height:1px}.h1,.h2,.h3,.h4,.h5,.h6,h1,h2,h3,h4,h5,h6{margin-top:0;margin-bottom:.5rem;font-weight:500;line-height:1.2}.h1,h1{font-size:calc(1.375rem + 1.5vw)}@media (min-width:1200px){.h1,h1{font-size:2.5rem}}.h2,h2{font-size:calc(1.325rem + .9vw)}@media (min-width:1200px){.h2,h2{font-size:2rem}}.h3,h3{font-size:calc(1.3rem + .6vw)}@media (min-width:1200px){.h3,h3{font-size:1.75rem}}.h4,h4{font-size:calc(1.275rem + .3vw)}@media (min-width:1200px){.h4,h4{font-size:1.5rem}}.h5,h5{font-size:1.25rem}.h6,h6{font-size:1rem}p{margin-top:0;margin-bottom:1rem}abbr[data-bs-original-title],abbr[title]{-webkit-text-decoration:underline dotted;text-decoration:underline dotted;cursor:help;-webkit-text-decoration-skip-ink:none;text-decoration-skip-ink:none}address{margin-bottom:1rem;font-style:normal;line-height:inherit}ol,ul{padding-left:2rem}dl,ol,ul{margin-top:0;margin-bottom:1rem}ol ol,ol ul,ul ol,ul ul{margin-bottom:0}dt{font-weight:700}dd{margin-bottom:.5rem;margin-left:0}blockquote{margin:0 0 1rem}b,strong{font-weight:bolder}.small,small{font-size:.875em}.mark,mark{padding:.2em;background-color:#fcf8e3}sub,sup{position:relative;font-size:.75em;line-height:0;vertical-align:baseline}sub{bottom:-.25em}sup{top:-.5em}a{color:#0d6efd;text-decoration:underline}a:hover{color:#0a58ca}a:not([href]):not([class]),a:not([href]):not([class]):hover{color:inherit;text-decoration:none}code,kbd,pre,samp{font-family:var(--bs-font-monospace);font-size:1em;direction:ltr;unicode-bidi:bidi-override}pre{display:block;margin-top:0;margin-bottom:1rem;overflow:auto;font-size:.875em}pre code{font-size:inherit;color:inherit;word-break:normal}code{font-size:.875em;color:#d63384;word-wrap:break-word}a>code{color:inherit}kbd{padding:.2rem .4rem;font-size:.875em;color:#fff;background-color:#212529;border-radius:.2rem}kbd kbd{padding:0;font-size:1em;font-weight:700}figure{margin:0 0 1rem}img,svg{vertical-align:middle}table{caption-side:bottom;border-collapse:collapse}caption{padding-top:.5rem;padding-bottom:.5rem;color:#6c757d;text-align:left}th{text-align:inherit;text-align:-webkit-match-parent}tbody,td,tfoot,th,thead,tr{border-color:inherit;border-style:solid;border-width:0}label{display:inline-block}button{border-radius:0}button:focus:not(:focus-visible){outline:0}button,input,optgroup,select,textarea{margin:0;font-family:inherit;font-size:inherit;line-height:inherit}button,select{text-transform:none}[role=button]{cursor:pointer}select{word-wrap:normal}select:disabled{opacity:1}[list]::-webkit-calendar-picker-indicator{display:none}[type=button],[type=reset],[type=submit],button{-webkit-appearance:button}[type=button]:not(:disabled),[type=reset]:not(:disabled),[type=submit]:not(:disabled),button:not(:disabled){cursor:pointer}::-moz-focus-inner{padding:0;border-style:none}textarea{resize:vertical}fieldset{min-width:0;padding:0;margin:0;border:0}legend{float:left;width:100%;padding:0;margin-bottom:.5rem;font-size:calc(1.275rem + .3vw);line-height:inherit}@media (min-width:1200px){legend{font-size:1.5rem}}legend+*{clear:left}::-webkit-datetime-edit-day-field,::-webkit-datetime-edit-fields-wrapper,::-webkit-datetime-edit-hour-field,::-webkit-datetime-edit-minute,::-webkit-datetime-edit-month-field,::-webkit-datetime-edit-text,::-webkit-datetime-edit-year-field{padding:0}::-webkit-inner-spin-button{height:auto}[type=
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     ]{outline-offset:-2px;-webkit-appearance:textfield}::-webkit-search-decoration{-webkit-appearance:none}::-webkit-color-swatch-wrapper{padding:0}::file-selector-button{font:inherit}::-webkit-file-upload-button{font:inherit;-webkit-appearance:button}output{display:inline-block}iframe{border:0}summary{display:list-item;cursor:pointer}progress{vertical-align:baseline}[hidden]{display:none!important}.lead{font-size:1.25rem;font-weight:300}.display-1{font-size:calc(1.625rem + 4.5vw);font-weight:300;line-height:1.2}@media (min-width:1200px){.display-1{font-size:5rem}}.display-2{font-size:calc(1.575rem + 3.9vw);font-weight:300;line-height:1.2}@media (min-width:1200px){.display-2{font-size:4.5rem}}.display-3{font-size:calc(1.525rem + 3.3vw);font-weight:300;line-height:1.2}@media (min-width:1200px){.display-3{font-size:4rem}}.display-4{font-size:calc(1.475rem + 2.7vw);font-weight:300;line-height:1.2}@media (min-width:1200px){.display-4{font-size:3.5rem}}.display-5{font-size:calc(1.425rem + 2.1vw);font-weight:300;line-height:1.2}@media (min-width:1200px){.display-5{font-size:3rem}}.display-6{font-size:calc(1.375rem + 1.5vw);font-weight:300;line-height:1.2}@media (min-width:1200px){.display-6{font-size:2.5rem}}.list-unstyled{padding-left:0;list-style:none}.list-inline{padding-left:0;list-style:none}.list-inline-item{display:inline-block}.list-inline-item:not(:last-child){margin-right:.5rem}.initialism{font-size:.875em;text-transform:uppercase}.blockquote{margin-bottom:1rem;font-size:1.25rem}.blockquote>:last-child{margin-bottom:0}.blockquote-footer{margin-top:-1rem;margin-bottom:1rem;font-size:.875em;color:#6c757d}.blockquote-footer::before{content:"— "}.img-fluid{max-width:100%;height:auto}.img-thumbnail{padding:.25rem;background-color:#fff;border:1px solid #dee2e6;border-radius:.25rem;max-width:100%;height:auto}.figure{display:inline-block}.figure-img{margin-bottom:.5rem;line-height:1}.figure-caption{font-size:.875em;color:#6c757d}.container,.container-fluid,.container-lg,.container-md,.container-sm,.container-xl,.container-xxl{width:100%;padding-right:var(--bs-gutter-x,.75rem);padding-left:var(--bs-gutter-x,.75rem);margin-right:auto;margin-left:auto}@media (min-width:576px){.container,.container-sm{max-width:540px}}@media (min-width:768px){.container,.container-md,.container-sm{max-width:720px}}@media (min-width:992px){.container,.container-lg,.container-md,.container-sm{max-width:960px}}@media (min-width:1200px){.container,.container-lg,.container-md,.container-sm,.container-xl{max-width:1140px}}@media (min-width:1400px){.container,.container-lg,.container-md,.container-sm,.container-xl,.container-xxl{max-width:1320px}}.row{--bs-gutter-x:1.5rem;--bs-gutter-y:0;display:flex;flex-wrap:wrap;margin-top:calc(var(--bs-gutter-y) * -1);margin-right:calc(var(--bs-gutter-x) * -.5);margin-left:calc(var(--bs-gutter-x) * -.5)}.row>*{flex-shrink:0;width:100%;max-width:100%;padding-right:calc(var(--bs-gutter-x) * .5);padding-left:calc(var(--bs-gutter-x) * .5);margin-top:var(--bs-gutter-y)}.col{flex:1 0 0%}.row-cols-auto>*{flex:0 0 auto;width:auto}.row-cols-1>*{flex:0 0 auto;width:100%}.row-cols-2>*{flex:0 0 auto;width:50%}.row-cols-3>*{flex:0 0 auto;width:33.3333333333%}.row-cols-4>*{flex:0 0 auto;width:25%}.row-cols-5>*{flex:0 0 auto;width:20%}.row-cols-6>*{flex:0 0 auto;width:16.6666666667%}.col-auto{flex:0 0 auto;width:auto}.col-1{flex:0 0 auto;width:8.33333333%}.col-2{flex:0 0 auto;width:16.66666667%}.col-3{flex:0 0 auto;width:25%}.col-4{flex:0 0 auto;width:33.33333333%}.col-5{flex:0 0 auto;width:41.66666667%}.col-6{flex:0 0 auto;width:50%}.col-7{flex:0 0 auto;width:58.33333333%}.col-8{flex:0 0 auto;width:66.66666667%}.col-9{flex:0 0 auto;width:75%}.col-10{flex:0 0 auto;width:83.33333333%}.col-11{flex:0 0 auto;width:91.66666667%}.col-12{flex:0 0 auto;width:100%}.offset-1{margin-left:8.33333333%}.offset-2{margin-left:16.66666667%}.offset-3{margin-left:25%}.offset-4{margin-left:33.33333333%}.offset-5{margin-left:41.66666667%}.offset-6{margin-left:50%}.offset-7{margin-left:58.33333333%}.offset-8{margin-left:66.66666667%}.offset-9{margin-left:75%}.offset-10{margin-left:83.33333333%}.offset-11{margin-left:91.66666667%}.g-0,.gx-0{--bs-gutter-x:0}.g-0,.gy-0{--bs-gutter-y:0}.g-1,.gx-1{--bs-gutter-x:0.25rem}.g-1,.gy-1{--bs-gutter-y:0.25rem}.g-2,.gx-2{--bs-gutter-x:0.5rem}.g-2,.gy-2{--bs-gutter-y:0.5rem}.g-3,.gx-3{--bs-gutter-x:1rem}.g-3,.gy-3{--bs-gutter-y:1rem}.g-4,.gx-4{--bs-gutter-x:1.5rem}.g-4,.gy-4{--bs-gutter-y:1.5rem}.g-5,.gx-5{--bs-gutter-x:3rem}.g-5,.gy-5{--bs-gutter-y:3rem}@media (min-width:576px){.col-sm{flex:1 0 0%}.row-cols-sm-auto>*{flex:0 0 auto;width:auto}.row-cols-sm-1>*{flex:0 0 auto;width:100%}.row-cols-sm-2>*{flex:0 0 auto;width:50%}.row-cols-sm-3>*{flex:0 0 auto;width:33.3333333333%}.row-cols-sm-4>*{flex:0 0 auto;width:25%}.row-cols-sm-5>*{flex:0 0 auto;width:20%}.row-cols-sm-6>*{flex:0 0 auto;width:16.6666666667%}.col-sm-auto{flex:0 0 auto;width:auto}.col-sm-1{flex:0 0 auto;width:8.33333333%}.col-sm-2{flex:0 0 auto;width:16.66666667%}.col-sm-3{flex:0 0 auto;width:25%}.col-sm-4{flex:0 0 auto;width:33.33333333%}.col-sm-5{flex:0 0 auto;width:41.66666667%}.col-sm-6{flex:0 0 auto;width:50%}.col-sm-7{flex:0 0 auto;width:58.33333333%}.col-sm-8{flex:0 0 auto;width:66.66666667%}.col-sm-9{flex:0 0 auto;width:75%}.col-sm-10{flex:0 0 auto;width:83.33333333%}.col-sm-11{flex:0 0 auto;width:91.66666667%}.col-sm-12{flex:0 0 auto;width:100%}.offset-sm-0{margin-left:0}.offset-sm-1{margin-left:8.33333333%}.offset-sm-2{margin-left:16.66666667%}.offset-sm-3{margin-left:25%}.offset-sm-4{margin-left:33.33333333%}.offset-sm-5{margin-left:41.66666667%}.offset-sm-6{margin-left:50%}.offset-sm-7{margin-left:58.33333333%}.offset-sm-8{margin-left:66.66666667%}.offset-sm-9{margin-left:75%}.offset-sm-10{margin-left:83.33333333%}.offset-sm-11{margin-left:91.66666667%}.g-sm-0,.gx-sm-0{--bs-gutter-x:0}.g-sm-0,.gy-sm-0{--bs-gutter-y:0}.g-sm-1,.gx-sm-1{--bs-gutter-x:0.25rem}.g-sm-1,.gy-sm-1{--bs-gutter-y:0.25rem}.g-sm-2,.gx-sm-2{--bs-gutter-x:0.5rem}.g-sm-2,.gy-sm-2{--bs-gutter-y:0.5rem}.g-sm-3,.gx-sm-3{--bs-gutter-x:1rem}.g-sm-3,.gy-sm-3{--bs-gutter-y:1rem}.g-sm-4,.gx-sm-4{--bs-gutter-x:1.5rem}.g-sm-4,.gy-sm-4{--bs-gutter-y:1.5rem}.g-sm-5,.gx-sm-5{--bs-gutter-x:3rem}.g-sm-5,.gy-sm-5{--bs-gutter-y:3rem}}@media (min-width:768px){.col-md{flex:1 0 0%}.row-cols-md-auto>*{flex:0 0 auto;width:auto}.row-cols-md-1>*{flex:0 0 auto;width:100%}.row-cols-md-2>*{flex:0 0 auto;width:50%}.row-cols-md-3>*{flex:0 0 auto;width:33.3333333333%}.row-cols-md-4>*{flex:0 0 auto;width:25%}.row-cols-md-5>*{flex:0 0 auto;width:20%}.row-cols-md-6>*{flex:0 0 auto;width:16.6666666667%}.col-md-auto{flex:0 0 auto;width:auto}.col-md-1{flex:0 0 auto;width:8.33333333%}.col-md-2{flex:0 0 auto;width:16.66666667%}.col-md-3{flex:0 0 auto;width:25%}.col-md-4{flex:0 0 auto;width:33.33333333%}.col-md-5{flex:0 0 auto;width:41.66666667%}.col-md-6{flex:0 0 auto;width:50%}.col-md-7{flex:0 0 auto;width:58.33333333%}.col-md-8{flex:0 0 auto;width:66.66666667%}.col-md-9{flex:0 0 auto;width:75%}.col-md-10{flex:0 0 auto;width:83.33333333%}.col-md-11{flex:0 0 auto;width:91.66666667%}.col-md-12{flex:0 0 auto;width:100%}.offset-md-0{margin-left:0}.offset-md-1{margin-left:8.33333333%}.offset-md-2{margin-left:16.66666667%}.offset-md-3{margin-left:25%}.offset-md-4{margin-left:33.33333333%}.offset-md-5{margin-left:41.66666667%}.offset-md-6{margin-left:50%}.offset-md-7{margin-left:58.33333333%}.offset-md-8{margin-left:66.66666667%}.offset-md-9{margin-left:75%}.offset-md-10{margin-left:83.33333333%}.offset-md-11{margin-left:91.66666667%}.g-md-0,.gx-md-0{--bs-gutter-x:0}.g-md-0,.gy-md-0{--bs-gutter-y:0}.g-md-1,.gx-md-1{--bs-gutter-x:0.25rem}.g-md-1,.gy-md-1{--bs-gutter-y:0.25rem}.g-md-2,.gx-md-2{--bs-gutter-x:0.5rem}.g-md-2,.gy-md-2{--bs-gutter-y:0.5rem}.g-md-3,.gx-md-3{--bs-gutter-x:1rem}.g-md-3,.gy-md-3{--bs-gutter-y:1rem}.g-md-4,.gx-md-4{--bs-gutter-x:1.5rem}.g-md-4,.gy-md-4{--bs-gutter-y:1.5rem}.g-md-5,.gx-md-5{--bs-gutter-x:3rem}.g-md-5,.gy-md-5{--bs-gutter-y:3rem}}@media (min-width:992px){.col-lg{flex:1 0 0%}.row-cols-lg-auto>*{flex:0 0 auto;width:auto}.row-cols-lg-1>*{flex:0 0 auto;width:100%}.row-cols-lg-2>*{flex:0 0 auto;width:50%}.row-cols-lg-3>*{flex:0 0 auto;width:33.3333333333%}.row-cols-lg-4>*{flex:0 0 auto;width:25%}.row-cols-lg-5>*{flex:0 0 auto;width:20%}.row-cols-lg-6>*{flex:0 0 auto;width:16.6666666667%}.col-lg-auto{flex:0 0 auto;width:auto}.col-lg-1{flex:0 0 auto;width:8.33333333%}.col-lg-2{flex:0 0 auto;width:16.66666667%}.col-lg-3{flex:0 0 auto;width:25%}.col-lg-4{flex:0 0 auto;width:33.33333333%}.col-lg-5{flex:0 0 auto;width:41.66666667%}.col-lg-6{flex:0 0 auto;width:50%}.col-lg-7{flex:0 0 auto;width:58.33333333%}.col-lg-8{flex:0 0 auto;width:66.66666667%}.col-lg-9{flex:0 0 auto;width:75%}.col-lg-10{flex:0 0 auto;width:83.33333333%}.col-lg-11{flex:0 0 auto;width:91.66666667%}.col-lg-12{flex:0 0 auto;width:100%}.offset-lg-0{margin-left:0}.offset-lg-1{margin-left:8.33333333%}.offset-lg-2{margin-left:16.66666667%}.offset-lg-3{margin-left:25%}.offset-lg-4{margin-left:33.33333333%}.offset-lg-5{margin-left:41.66666667%}.offset-lg-6{margin-left:50%}.offset-lg-7{margin-left:58.33333333%}.offset-lg-8{margin-left:66.66666667%}.offset-lg-9{margin-left:75%}.offset-lg-10{margin-left:83.33333333%}.offset-lg-11{margin-left:91.66666667%}.g-lg-0,.gx-lg-0{--bs-gutter-x:0}.g-lg-0,.gy-lg-0{--bs-gutter-y:0}.g-lg-1,.gx-lg-1{--bs-gutter-x:0.25rem}.g-lg-1,.gy-lg-1{--bs-gutter-y:0.25rem}.g-lg-2,.gx-lg-2{--bs-gutter-x:0.5rem}.g-lg-2,.gy-lg-2{--bs-gutter-y:0.5rem}.g-lg-3,.gx-lg-3{--bs-gutter-x:1rem}.g-lg-3,.gy-lg-3{--bs-gutter-y:1rem}.g-lg-4,.gx-lg-4{--bs-gutter-x:1.5rem}.g-lg-4,.gy-lg-4{--bs-gutter-y:1.5rem}.g-lg-5,.gx-lg-5{--bs-gutter-x:3rem}.g-lg-5,.gy-lg-5{--bs-gutter-y:3rem}}@media (min-width:1200px){.col-xl{flex:1 0 0%}.row-cols-xl-auto>*{flex:0 0 auto;width:auto}.row-cols-xl-1>*{flex:0 0 auto;width:100%}.row-cols-xl-2>*{flex:0 0 auto;width:50%}.row-cols-xl-3>*{flex:0 0 auto;width:33.3333333333%}.row-cols-xl-4>*{flex:0 0 auto;width:25%}.row-cols-xl-5>*{flex:0 0 auto;width:20%}.row-cols-xl-6>*{flex:0 0 auto;width:16.6666666667%}.col-xl-auto{flex:0 0 auto;width:auto}.col-xl-1{flex:0 0 auto;width:8.33333333%}.col-xl-2{flex:0 0 auto;width:16.66666667%}.col-xl-3{flex:0 0 auto;width:25%}.col-xl-4{flex:0 0 auto;width:33.33333333%}.col-xl-5{flex:0 0 auto;width:41.66666667%}.col-xl-6{flex:0 0 auto;width:50%}.col-xl-7{flex:0 0 auto;width:58.33333333%}.col-xl-8{flex:0 0 auto;width:66.66666667%}.col-xl-9{flex:0 0 auto;width:75%}.col-xl-10{flex:0 0 auto;width:83.33333333%}.col-xl-11{flex:0 0 auto;width:91.66666667%}.col-xl-12{flex:0 0 auto;width:100%}.offset-xl-0{margin-left:0}.offset-xl-1{margin-left:8.33333333%}.offset-xl-2{margin-left:16.66666667%}.offset-xl-3{margin-left:25%}.offset-xl-4{margin-left:33.33333333%}.offset-xl-5{margin-left:41.66666667%}.offset-xl-6{margin-left:50%}.offset-xl-7{margin-left:58.33333333%}.offset-xl-8{margin-left:66.66666667%}.offset-xl-9{margin-left:75%}.offset-xl-10{margin-left:83.33333333%}.offset-xl-11{margin-left:91.66666667%}.g-xl-0,.gx-xl-0{--bs-gutter-x:0}.g-xl-0,.gy-xl-0{--bs-gutter-y:0}.g-xl-1,.gx-xl-1{--bs-gutter-x:0.25rem}.g-xl-1,.gy-xl-1{--bs-gutter-y:0.25rem}.g-xl-2,.gx-xl-2{--bs-gutter-x:0.5rem}.g-xl-2,.gy-xl-2{--bs-gutter-y:0.5rem}.g-xl-3,.gx-xl-3{--bs-gutter-x:1rem}.g-xl-3,.gy-xl-3{--bs-gutter-y:1rem}.g-xl-4,.gx-xl-4{--bs-gutter-x:1.5rem}.g-xl-4,.gy-xl-4{--bs-gutter-y:1.5rem}.g-xl-5,.gx-xl-5{--bs-gutter-x:3rem}.g-xl-5,.gy-xl-5{--bs-gutter-y:3rem}}@media (min-width:1400px){.col-xxl{flex:1 0 0%}.row-cols-xxl-auto>*{flex:0 0 auto;width:auto}.row-cols-xxl-1>*{flex:0 0 auto;width:100%}.row-cols-xxl-2>*{flex:0 0 auto;width:50%}.row-cols-xxl-3>*{flex:0 0 auto;width:33.3333333333%}.row-cols-xxl-4>*{flex:0 0 auto;width:25%}.row-cols-xxl-5>*{flex:0 0 auto;width:20%}.row-cols-xxl-6>*{flex:0 0 auto;width:16.6666666667%}.col-xxl-auto{flex:0 0 auto;width:auto}.col-xxl-1{flex:0 0 auto;width:8.33333333%}.col-xxl-2{flex:0 0 auto;width:16.66666667%}.col-xxl-3{flex:0 0 auto;width:25%}.col-xxl-4{flex:0 0 auto;width:33.33333333%}.col-xxl-5{flex:0 0 auto;width:41.66666667%}.col-xxl-6{flex:0 0 auto;width:50%}.col-xxl-7{flex:0 0 auto;width:58.33333333%}.col-xxl-8{flex:0 0 auto;width:66.66666667%}.col-xxl-9{flex:0 0 auto;width:75%}.col-xxl-10{flex:0 0 auto;width:83.33333333%}.col-xxl-11{flex:0 0 auto;width:91.66666667%}.col-xxl-12{flex:0 0 auto;width:100%}.offset-xxl-0{margin-left:0}.offset-xxl-1{margin-left:8.33333333%}.offset-xxl-2{margin-left:16.66666667%}.offset-xxl-3{margin-left:25%}.offset-xxl-4{margin-left:33.33333333%}.offset-xxl-5{margin-left:41.66666667%}.offset-xxl-6{margin-left:50%}.offset-xxl-7{margin-left:58.33333333%}.offset-xxl-8{margin-left:66.66666667%}.offset-xxl-9{margin-left:75%}.offset-xxl-10{margin-left:83.33333333%}.offset-xxl-11{margin-left:91.66666667%}.g-xxl-0,.gx-xxl-0{--bs-gutter-x:0}.g-xxl-0,.gy-xxl-0{--bs-gutter-y:0}.g-xxl-1,.gx-xxl-1{--bs-gutter-x:0.25rem}.g-xxl-1,.gy-xxl-1{--bs-gutter-y:0.25rem}.g-xxl-2,.gx-xxl-2{--bs-gutter-x:0.5rem}.g-xxl-2,.gy-xxl-2{--bs-gutter-y:0.5rem}.g-xxl-3,.gx-xxl-3{--bs-gutter-x:1rem}.g-xxl-3,.gy-xxl-3{--bs-gutter-y:1rem}.g-xxl-4,.gx-xxl-4{--bs-gutter-x:1.5rem}.g-xxl-4,.gy-xxl-4{--bs-gutter-y:1.5rem}.g-xxl-5,.gx-xxl-5{--bs-gutter-x:3rem}.g-xxl-5,.gy-xxl-5{--bs-gutter-y:3rem}}.table{--bs-table-bg:transparent;--bs-table-accent-bg:transparent;--bs-table-striped-color:#212529;--bs-table-striped-bg:rgba(0, 0, 0, 0.05);--bs-table-active-color:#212529;--bs-table-active-bg:rgba(0, 0, 0, 0.1);--bs-table-hover-color:#212529;--bs-table-hover-bg:rgba(0, 0, 0, 0.075);width:100%;margin-bottom:1rem;color:#212529;vertical-align:top;border-color:#dee2e6}.table>:not(caption)>*>*{padding:.5rem .5rem;background-color:var(--bs-table-bg);border-bottom-width:1px;box-shadow:inset 0 0 0 9999px var(--bs-table-accent-bg)}.table>tbody{vertical-align:inherit}.table>thead{vertical-align:bottom}.table>:not(:last-child)>:last-child>*{border-bottom-color:currentColor}.caption-top{caption-side:top}.table-sm>:not(caption)>*>*{padding:.25rem .25rem}.table-bordered>:not(caption)>*{border-width:1px 0}.table-bordered>:not(caption)>*>*{border-width:0 1px}.table-borderless>:not(caption)>*>*{border-bottom-width:0}.table-striped>tbody>tr:nth-of-type(odd){--bs-table-accent-bg:var(--bs-table-striped-bg);color:var(--bs-table-striped-color)}.table-active{--bs-table-accent-bg:var(--bs-table-active-bg);color:var(--bs-table-active-color)}.table-hover>tbody>tr:hover{--bs-table-accent-bg:var(--bs-table-hover-bg);color:var(--bs-table-hover-color)}.table-primary{--bs-table-bg:#cfe2ff;--bs-table-striped-bg:#c5d7f2;--bs-table-striped-color:#000;--bs-table-active-bg:#bacbe6;--bs-table-active-color:#000;--bs-table-hover-bg:#bfd1ec;--bs-table-hover-color:#000;color:#000;border-color:#bacbe6}.table-secondary{--bs-table-bg:#e2e3e5;--bs-table-striped-bg:#d7d8da;--bs-table-striped-color:#000;--bs-table-active-bg:#cbccce;--bs-table-active-color:#000;--bs-table-hover-bg:#d1d2d4;--bs-table-hover-color:#000;color:#000;border-color:#cbccce}.table-success{--bs-table-bg:#d1e7dd;--bs-table-striped-bg:#c7dbd2;--bs-table-striped-color:#000;--bs-table-active-bg:#bcd0c7;--bs-table-active-color:#000;--bs-table-hover-bg:#c1d6cc;--bs-table-hover-color:#000;color:#000;border-color:#bcd0c7}.table-info{--bs-table-bg:#cff4fc;--bs-table-striped-bg:#c5e8ef;--bs-table-striped-color:#000;--bs-table-active-bg:#badce3;--bs-table-active-color:#000;--bs-table-hover-bg:#bfe2e9;--bs-table-hover-color:#000;color:#000;border-color:#badce3}.table-warning{--bs-table-bg:#fff3cd;--bs-table-striped-bg:#f2e7c3;--bs-table-striped-color:#000;--bs-table-active-bg:#e6dbb9;--bs-table-active-color:#000;--bs-table-hover-bg:#ece1be;--bs-table-hover-color:#000;color:#000;border-color:#e6dbb9}.table-danger{--bs-table-bg:#f8d7da;--bs-table-striped-bg:#eccccf;--bs-table-striped-color:#000;--bs-table-active-bg:#dfc2c4;--bs-table-active-color:#000;--bs-table-hover-bg:#e5c7ca;--bs-table-hover-color:#000;color:#000;border-color:#dfc2c4}.table-light{--bs-table-bg:#f8f9fa;--bs-table-striped-bg:#ecedee;--bs-table-striped-color:#000;--bs-table-active-bg:#dfe0e1;--bs-table-active-color:#000;--bs-table-hover-bg:#e5e6e7;--bs-table-hover-color:#000;color:#000;border-color:#dfe0e1}.table-dark{--bs-table-bg:#212529;--bs-table-striped-bg:#2c3034;--bs-table-striped-color:#fff;--bs-table-active-bg:#373b3e;--bs-table-active-color:#fff;--bs-table-hover-bg:#323539;--bs-table-hover-color:#fff;color:#fff;border-color:#373b3e}.table-responsive{overflow-x:auto;-webkit-overflow-scrolling:touch}@media (max-width:575.98px){.table-responsive-sm{overflow-x:auto;-webkit-overflow-scrolling:touch}}@media (max-width:767.98px){.table-responsive-md{overflow-x:auto;-webkit-overflow-scrolling:touch}}@media (max-width:991.98px){.table-responsive-lg{overflow-x:auto;-webkit-overflow-scrolling:touch}}@media (max-width:1199.98px){.table-responsive-xl{overflow-x:auto;-webkit-overflow-scrolling:touch}}@media (max-width:1399.98px){.table-responsive-xxl{overflow-x:auto;-webkit-overflow-scrolling:touch}}.form-label{margin-bottom:.5rem}.col-form-label{padding-top:calc(.375rem + 1px);padding-bottom:calc(.375rem + 1px);margin-bottom:0;font-size:inherit;line-height:1.5}.col-form-label-lg{padding-top:calc(.5rem + 1px);padding-bottom:calc(.5rem + 1px);font-size:1.25rem}.col-form-label-sm{padding-top:calc(.25rem + 1px);padding-bottom:calc(.25rem + 1px);font-size:.875rem}.form-text{margin-top:.25rem;font-size:.875em;color:#6c757d}.form-control{display:block;width:100%;padding:.375rem .75rem;font-size:1rem;font-weight:400;line-height:1.5;color:#212529;background-color:#fff;background-clip:padding-box;border:1px solid #ced4da;-webkit-appearance:none;-moz-appearance:none;appearance:none;border-radius:.25rem;transition:border-color .15s ease-in-out,box-shadow .15s ease-in-out}@media (prefers-reduced-motion:reduce){.form-control{transition:none}}.form-control[type=file]{overflow:hidden}.form-control[type=file]:not(:disabled):not([readonly]){cursor:pointer}.form-control:focus{color:#212529;background-color:#fff;border-color:#86b7fe;outline:0;box-shadow:0 0 0 .25rem rgba(13,110,253,.25)}.form-control::-webkit-date-and-time-value{height:1.5em}.form-control::-moz-placeholder{color:#6c757d;opacity:1}.form-control::placeholder{color:#6c757d;opacity:1}.form-control:disabled,.form-control[readonly]{background-color:#e9ecef;opacity:1}.form-control::file-selector-button{padding:.375rem .75rem;margin:-.375rem -.75rem;-webkit-margin-end:.75rem;margin-inline-end:.75rem;color:#212529;background-color:#e9ecef;pointer-events:none;border-color:inherit;border-style:solid;border-width:0;border-inline-end-width:1px;border-radius:0;transition:color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out}@media (prefers-reduced-motion:reduce){.form-control::file-selector-button{transition:none}}.form-control:hover:not(:disabled):not([readonly])::file-selector-button{background-color:#dde0e3}.form-control::-webkit-file-upload-button{padding:.375rem .75rem;margin:-.375rem -.75rem;-webkit-margin-end:.75rem;margin-inline-end:.75rem;color:#212529;background-color:#e9ecef;pointer-events:none;border-color:inherit;border-style:solid;border-width:0;border-inline-end-width:1px;border-radius:0;-webkit-transition:color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out;transition:color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out}@media (prefers-reduced-motion:reduce){.form-control::-webkit-file-upload-button{-webkit-transition:none;transition:none}}.form-control:hover:not(:disabled):not([readonly])::-webkit-file-upload-button{background-color:#dde0e3}.form-control-plaintext{display:block;width:100%;padding:.375rem 0;margin-bottom:0;line-height:1.5;color:#212529;background-color:transparent;border:solid transparent;border-width:1px 0}.form-control-plaintext.form-control-lg,.form-control-plaintext.form-control-sm{padding-right:0;padding-left:0}.form-control-sm{min-height:calc(1.5em + .5rem + 2px);padding:.25rem .5rem;font-size:.875rem;border-radius:.2rem}.form-control-sm::file-selector-button{padding:.25rem .5rem;margin:-.25rem -.5rem;-webkit-margin-end:.5rem;margin-inline-end:.5rem}.form-control-sm::-webkit-file-upload-button{padding:.25rem .5rem;margin:-.25rem -.5rem;-webkit-margin-end:.5rem;margin-inline-end:.5rem}.form-control-lg{min-height:calc(1.5em + 1rem + 2px);padding:.5rem 1rem;font-size:1.25rem;border-radius:.3rem}.form-control-lg::file-selector-button{padding:.5rem 1rem;margin:-.5rem -1rem;-webkit-margin-end:1rem;margin-inline-end:1rem}.form-control-lg::-webkit-file-upload-button{padding:.5rem 1rem;margin:-.5rem -1rem;-webkit-margin-end:1rem;margin-inline-end:1rem}textarea.form-control{min-height:calc(1.5em + .75rem + 2px)}textarea.form-control-sm{min-height:calc(1.5em + .5rem + 2px)}textarea.form-control-lg{min-height:calc(1.5em + 1rem + 2px)}.form-control-color{width:3rem;height:auto;padding:.375rem}.form-control-color:not(:disabled):not([readonly]){cursor:pointer}.form-control-color::-moz-color-swatch{height:1.5em;border-radius:.25rem}.form-control-color::-webkit-color-swatch{height:1.5em;border-radius:.25rem}.form-select{display:block;width:100%;padding:.375rem 2.25rem .375rem .75rem;-moz-padding-start:calc(0.75rem - 3px);font-size:1rem;font-weight:400;line-height:1.5;color:#212529;background-color:#fff;background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3e%3cpath fill='none' stroke='%23343a40' stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M2 5l6 6 6-6'/%3e%3c/svg%3e");background-repeat:no-repeat;background-position:right .75rem center;background-size:16px 12px;border:1px solid #ced4da;border-radius:.25rem;transition:border-color .15s ease-in-out,box-shadow .15s ease-in-out;-webkit-appearance:none;-moz-appearance:none;appearance:none}@media (prefers-reduced-motion:reduce){.form-select{transition:none}}.form-select:focus{border-color:#86b7fe;outline:0;box-shadow:0 0 0 .25rem rgba(13,110,253,.25)}.form-select[multiple],.form-select[size]:not([size="1"]){padding-right:.75rem;background-image:none}.form-select:disabled{background-color:#e9ecef}.form-select:-moz-focusring{color:transparent;text-shadow:0 0 0 #212529}.form-select-sm{padding-top:.25rem;padding-bottom:.25rem;padding-left:.5rem;font-size:.875rem}.form-select-lg{padding-top:.5rem;padding-bottom:.5rem;padding-left:1rem;font-size:1.25rem}.form-check{display:block;min-height:1.5rem;padding-left:1.5em;margin-bottom:.125rem}.form-check .form-check-input{float:left;margin-left:-1.5em}.form-check-input{width:1em;height:1em;margin-top:.25em;vertical-align:top;background-color:#fff;background-repeat:no-repeat;background-position:center;background-size:contain;border:1px solid rgba(0,0,0,.25);-webkit-appearance:none;-moz-appearance:none;appearance:none;-webkit-print-color-adjust:exact;color-adjust:exact}.form-check-input[type=checkbox]{border-radius:.25em}.form-check-input[type=radio]{border-radius:50%}.form-check-input:active{filter:brightness(90%)}.form-check-input:focus{border-color:#86b7fe;outline:0;box-shadow:0 0 0 .25rem rgba(13,110,253,.25)}.form-check-input:checked{background-color:#0d6efd;border-color:#0d6efd}.form-check-input:checked[type=checkbox]{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'%3e%3cpath fill='none' stroke='%23fff' stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='M6 10l3 3l6-6'/%3e%3c/svg%3e")}.form-check-input:checked[type=radio]{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='-4 -4 8 8'%3e%3ccircle r='2' fill='%23fff'/%3e%3c/svg%3e")}.form-check-input[type=checkbox]:indeterminate{background-color:#0d6efd;border-color:#0d6efd;background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'%3e%3cpath fill='none' stroke='%23fff' stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='M6 10h8'/%3e%3c/svg%3e")}.form-check-input:disabled{pointer-events:none;filter:none;opacity:.5}.form-check-input:disabled~.form-check-label,.form-check-input[disabled]~.form-check-label{opacity:.5}.form-switch{padding-left:2.5em}.form-switch .form-check-input{width:2em;margin-left:-2.5em;background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='-4 -4 8 8'%3e%3ccircle r='3' fill='rgba%280, 0, 0, 0.25%29'/%3e%3c/svg%3e");background-position:left center;border-radius:2em;transition:background-position .15s ease-in-out}@media (prefers-reduced-motion:reduce){.form-switch .form-check-input{transition:none}}.form-switch .form-check-input:focus{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='-4 -4 8 8'%3e%3ccircle r='3' fill='%2386b7fe'/%3e%3c/svg%3e")}.form-switch .form-check-input:checked{background-position:right center;background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='-4 -4 8 8'%3e%3ccircle r='3' fill='%23fff'/%3e%3c/svg%3e")}.form-check-inline{display:inline-block;margin-right:1rem}.btn-check{position:absolute;clip:rect(0,0,0,0);pointer-events:none}.btn-check:disabled+.btn,.btn-check[disabled]+.btn{pointer-events:none;filter:none;opacity:.65}.form-range{width:100%;height:1.5rem;padding:0;background-color:transparent;-webkit-appearance:none;-moz-appearance:none;appearance:none}.form-range:focus{outline:0}.form-range:focus::-webkit-slider-thumb{box-shadow:0 0 0 1px #fff,0 0 0 .25rem rgba(13,110,253,.25)}.form-range:focus::-moz-range-thumb{box-shadow:0 0 0 1px #fff,0 0 0 .25rem rgba(13,110,253,.25)}.form-range::-moz-focus-outer{border:0}.form-range::-webkit-slider-thumb{width:1rem;height:1rem;margin-top:-.25rem;background-color:#0d6efd;border:0;border-radius:1rem;-webkit-transition:background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out;transition:background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out;-webkit-appearance:none;appearance:none}@media (prefers-reduced-motion:reduce){.form-range::-webkit-slider-thumb{-webkit-transition:none;transition:none}}.form-range::-webkit-slider-thumb:active{background-color:#b6d4fe}.form-range::-webkit-slider-runnable-track{width:100%;height:.5rem;color:transparent;cursor:pointer;background-color:#dee2e6;border-color:transparent;border-radius:1rem}.form-range::-moz-range-thumb{width:1rem;height:1rem;background-color:#0d6efd;border:0;border-radius:1rem;-moz-transition:background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out;transition:background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out;-moz-appearance:none;appearance:none}@media (prefers-reduced-motion:reduce){.form-range::-moz-range-thumb{-moz-transition:none;transition:none}}.form-range::-moz-range-thumb:active{background-color:#b6d4fe}.form-range::-moz-range-track{width:100%;height:.5rem;color:transparent;cursor:pointer;background-color:#dee2e6;border-color:transparent;border-radius:1rem}.form-range:disabled{pointer-events:none}.form-range:disabled::-webkit-slider-thumb{background-color:#adb5bd}.form-range:disabled::-moz-range-thumb{background-color:#adb5bd}.form-floating{position:relative}.form-floating>.form-control,.form-floating>.form-select{height:calc(3.5rem + 2px);line-height:1.25}.form-floating>label{position:absolute;top:0;left:0;height:100%;padding:1rem .75rem;pointer-events:none;border:1px solid transparent;transform-origin:0 0;transition:opacity .1s ease-in-out,transform .1s ease-in-out}@media (prefers-reduced-motion:reduce){.form-floating>label{transition:none}}.form-floating>.form-control{padding:1rem .75rem}.form-floating>.form-control::-moz-placeholder{color:transparent}.form-floating>.form-control::placeholder{color:transparent}.form-floating>.form-control:not(:-moz-placeholder-shown){padding-top:1.625rem;padding-bottom:.625rem}.form-floating>.form-control:focus,.form-floating>.form-control:not(:placeholder-shown){padding-top:1.625rem;padding-bottom:.625rem}.form-floating>.form-control:-webkit-autofill{padding-top:1.625rem;padding-bottom:.625rem}.form-floating>.form-select{padding-top:1.625rem;padding-bottom:.625rem}.form-floating>.form-control:not(:-moz-placeholder-shown)~label{opacity:.65;transform:scale(.85) translateY(-.5rem) translateX(.15rem)}.form-floating>.form-control:focus~label,.form-floating>.form-control:not(:placeholder-shown)~label,.form-floating>.form-select~label{opacity:.65;transform:scale(.85) translateY(-.5rem) translateX(.15rem)}.form-floating>.form-control:-webkit-autofill~label{opacity:.65;transform:scale(.85) translateY(-.5rem) translateX(.15rem)}.input-group{position:relative;display:flex;flex-wrap:wrap;align-items:stretch;width:100%}.input-group>.form-control,.input-group>.form-select{position:relative;flex:1 1 auto;width:1%;min-width:0}.input-group>.form-control:focus,.input-group>.form-select:focus{z-index:3}.input-group .btn{position:relative;z-index:2}.input-group .btn:focus{z-index:3}.input-group-text{display:flex;align-items:center;padding:.375rem .75rem;font-size:1rem;font-weight:400;line-height:1.5;color:#212529;text-align:center;white-space:nowrap;background-color:#e9ecef;border:1px solid #ced4da;border-radius:.25rem}.input-group-lg>.btn,.input-group-lg>.form-control,.input-group-lg>.form-select,.input-group-lg>.input-group-text{padding:.5rem 1rem;font-size:1.25rem;border-radius:.3rem}.input-group-sm>.btn,.input-group-sm>.form-control,.input-group-sm>.form-select,.input-group-sm>.input-group-text{padding:.25rem .5rem;font-size:.875rem;border-radius:.2rem}.input-group-lg>.form-select,.input-group-sm>.form-select{padding-right:3rem}.input-group:not(.has-validation)>.dropdown-toggle:nth-last-child(n+3),.input-group:not(.has-validation)>:not(:last-child):not(.dropdown-toggle):not(.dropdown-menu){border-top-right-radius:0;border-bottom-right-radius:0}.input-group.has-validation>.dropdown-toggle:nth-last-child(n+4),.input-group.has-validation>:nth-last-child(n+3):not(.dropdown-toggle):not(.dropdown-menu){border-top-right-radius:0;border-bottom-right-radius:0}.input-group>:not(:first-child):not(.dropdown-menu):not(.valid-tooltip):not(.valid-feedback):not(.invalid-tooltip):not(.invalid-feedback){margin-left:-1px;border-top-left-radius:0;border-bottom-left-radius:0}.valid-feedback{display:none;width:100%;margin-top:.25rem;font-size:.875em;color:#198754}.valid-tooltip{position:absolute;top:100%;z-index:5;display:none;max-width:100%;padding:.25rem .5rem;margin-top:.1rem;font-size:.875rem;color:#fff;background-color:rgba(25,135,84,.9);border-radius:.25rem}.is-valid~.valid-feedback,.is-valid~.valid-tooltip,.was-validated :valid~.valid-feedback,.was-validated :valid~.valid-tooltip{display:block}.form-control.is-valid,.was-validated .form-control:valid{border-color:#198754;padding-right:calc(1.5em + .75rem);background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 8 8'%3e%3cpath fill='%23198754' d='M2.3 6.73L.6 4.53c-.4-1.04.46-1.4 1.1-.8l1.1 1.4 3.4-3.8c.6-.63 1.6-.27 1.2.7l-4 4.6c-.43.5-.8.4-1.1.1z'/%3e%3c/svg%3e");background-repeat:no-repeat;background-position:right calc(.375em + .1875rem) center;background-size:calc(.75em + .375rem) calc(.75em + .375rem)}.form-control.is-valid:focus,.was-validated .form-control:valid:focus{border-color:#198754;box-shadow:0 0 0 .25rem rgba(25,135,84,.25)}.was-validated textarea.form-control:valid,textarea.form-control.is-valid{padding-right:calc(1.5em + .75rem);background-position:top calc(.375em + .1875rem) right calc(.375em + .1875rem)}.form-select.is-valid,.was-validated .form-select:valid{border-color:#198754}.form-select.is-valid:not([multiple]):not([size]),.form-select.is-valid:not([multiple])[size="1"],.was-validated .form-select:valid:not([multiple]):not([size]),.was-validated .form-select:valid:not([multiple])[size="1"]{padding-right:4.125rem;background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3e%3cpath fill='none' stroke='%23343a40' stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M2 5l6 6 6-6'/%3e%3c/svg%3e"),url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 8 8'%3e%3cpath fill='%23198754' d='M2.3 6.73L.6 4.53c-.4-1.04.46-1.4 1.1-.8l1.1 1.4 3.4-3.8c.6-.63 1.6-.27 1.2.7l-4 4.6c-.43.5-.8.4-1.1.1z'/%3e%3c/svg%3e");background-position:right .75rem center,center right 2.25rem;background-size:16px 12px,calc(.75em + .375rem) calc(.75em + .375rem)}.form-select.is-valid:focus,.was-validated .form-select:valid:focus{border-color:#198754;box-shadow:0 0 0 .25rem rgba(25,135,84,.25)}.form-check-input.is-valid,.was-validated .form-check-input:valid{border-color:#198754}.form-check-input.is-valid:checked,.was-validated .form-check-input:valid:checked{background-color:#198754}.form-check-input.is-valid:focus,.was-validated .form-check-input:valid:focus{box-shadow:0 0 0 .25rem rgba(25,135,84,.25)}.form-check-input.is-valid~.form-check-label,.was-validated .form-check-input:valid~.form-check-label{color:#198754}.form-check-inline .form-check-input~.valid-feedback{margin-left:.5em}.input-group .form-control.is-valid,.input-group .form-select.is-valid,.was-validated .input-group .form-control:valid,.was-validated .input-group .form-select:valid{z-index:1}.input-group .form-control.is-valid:focus,.input-group .form-select.is-valid:focus,.was-validated .input-group .form-control:valid:focus,.was-validated .input-group .form-select:valid:focus{z-index:3}.invalid-feedback{display:none;width:100%;margin-top:.25rem;font-size:.875em;color:#dc3545}.invalid-tooltip{position:absolute;top:100%;z-index:5;display:none;max-width:100%;padding:.25rem .5rem;margin-top:.1rem;font-size:.875rem;color:#fff;background-color:rgba(220,53,69,.9);border-radius:.25rem}.is-invalid~.invalid-feedback,.is-invalid~.invalid-tooltip,.was-validated :invalid~.invalid-feedback,.was-validated :invalid~.invalid-tooltip{display:block}.form-control.is-invalid,.was-validated .form-control:invalid{border-color:#dc3545;padding-right:calc(1.5em + .75rem);background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 12 12' width='12' height='12' fill='none' stroke='%23dc3545'%3e%3ccircle cx='6' cy='6' r='4.5'/%3e%3cpath stroke-linejoin='round' d='M5.8 3.6h.4L6 6.5z'/%3e%3ccircle cx='6' cy='8.2' r='.6' fill='%23dc3545' stroke='none'/%3e%3c/svg%3e");background-repeat:no-repeat;background-position:right calc(.375em + .1875rem) center;background-size:calc(.75em + .375rem) calc(.75em + .375rem)}.form-control.is-invalid:focus,.was-validated .form-control:invalid:focus{border-color:#dc3545;box-shadow:0 0 0 .25rem rgba(220,53,69,.25)}.was-validated textarea.form-control:invalid,textarea.form-control.is-invalid{padding-right:calc(1.5em + .75rem);background-position:top calc(.375em + .1875rem) right calc(.375em + .1875rem)}.form-select.is-invalid,.was-validated .form-select:invalid{border-color:#dc3545}.form-select.is-invalid:not([multiple]):not([size]),.form-select.is-invalid:not([multiple])[size="1"],.was-validated .form-select:invalid:not([multiple]):not([size]),.was-validated .form-select:invalid:not([multiple])[size="1"]{padding-right:4.125rem;background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'%3e%3cpath fill='none' stroke='%23343a40' stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M2 5l6 6 6-6'/%3e%3c/svg%3e"),url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 12 12' width='12' height='12' fill='none' stroke='%23dc3545'%3e%3ccircle cx='6' cy='6' r='4.5'/%3e%3cpath stroke-linejoin='round' d='M5.8 3.6h.4L6 6.5z'/%3e%3ccircle cx='6' cy='8.2' r='.6' fill='%23dc3545' stroke='none'/%3e%3c/svg%3e");background-position:right .75rem center,center right 2.25rem;background-size:16px 12px,calc(.75em + .375rem) calc(.75em + .375rem)}.form-select.is-invalid:focus,.was-validated .form-select:invalid:focus{border-color:#dc3545;box-shadow:0 0 0 .25rem rgba(220,53,69,.25)}.form-check-input.is-invalid,.was-validated .form-check-input:invalid{border-color:#dc3545}.form-check-input.is-invalid:checked,.was-validated .form-check-input:invalid:checked{background-color:#dc3545}.form-check-input.is-invalid:focus,.was-validated .form-check-input:invalid:focus{box-shadow:0 0 0 .25rem rgba(220,53,69,.25)}.form-check-input.is-invalid~.form-check-label,.was-validated .form-check-input:invalid~.form-check-label{color:#dc3545}.form-check-inline .form-check-input~.invalid-feedback{margin-left:.5em}.input-group .form-control.is-invalid,.input-group .form-select.is-invalid,.was-validated .input-group .form-control:invalid,.was-validated .input-group .form-select:invalid{z-index:2}.input-group .form-control.is-invalid:focus,.input-group .form-select.is-invalid:focus,.was-validated .input-group .form-control:invalid:focus,.was-validated .input-group .form-select:invalid:focus{z-index:3}.btn{display:inline-block;font-weight:400;line-height:1.5;color:#212529;text-align:center;text-decoration:none;vertical-align:middle;cursor:pointer;-webkit-user-select:none;-moz-user-select:none;user-select:none;background-color:transparent;border:1px solid transparent;padding:.375rem .75rem;font-size:1rem;border-radius:.25rem;transition:color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out}@media (prefers-reduced-motion:reduce){.btn{transition:none}}.btn:hover{color:#212529}.btn-check:focus+.btn,.btn:focus{outline:0;box-shadow:0 0 0 .25rem rgba(13,110,253,.25)}.btn.disabled,.btn:disabled,fieldset:disabled .btn{pointer-events:none;opacity:.65}.btn-primary{color:#fff;background-color:#0d6efd;border-color:#0d6efd}.btn-primary:hover{color:#fff;background-color:#0b5ed7;border-color:#0a58ca}.btn-check:focus+.btn-primary,.btn-primary:focus{color:#fff;background-color:#0b5ed7;border-color:#0a58ca;box-shadow:0 0 0 .25rem rgba(49,132,253,.5)}.btn-check:active+.btn-primary,.btn-check:checked+.btn-primary,.btn-primary.active,.btn-primary:active,.show>.btn-primary.dropdown-toggle{color:#fff;background-color:#0a58ca;border-color:#0a53be}.btn-check:active+.btn-primary:focus,.btn-check:checked+.btn-primary:focus,.btn-primary.active:focus,.btn-primary:active:focus,.show>.btn-primary.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(49,132,253,.5)}.btn-primary.disabled,.btn-primary:disabled{color:#fff;background-color:#0d6efd;border-color:#0d6efd}.btn-secondary{color:#fff;background-color:#6c757d;border-color:#6c757d}.btn-secondary:hover{color:#fff;background-color:#5c636a;border-color:#565e64}.btn-check:focus+.btn-secondary,.btn-secondary:focus{color:#fff;background-color:#5c636a;border-color:#565e64;box-shadow:0 0 0 .25rem rgba(130,138,145,.5)}.btn-check:active+.btn-secondary,.btn-check:checked+.btn-secondary,.btn-secondary.active,.btn-secondary:active,.show>.btn-secondary.dropdown-toggle{color:#fff;background-color:#565e64;border-color:#51585e}.btn-check:active+.btn-secondary:focus,.btn-check:checked+.btn-secondary:focus,.btn-secondary.active:focus,.btn-secondary:active:focus,.show>.btn-secondary.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(130,138,145,.5)}.btn-secondary.disabled,.btn-secondary:disabled{color:#fff;background-color:#6c757d;border-color:#6c757d}.btn-success{color:#fff;background-color:#198754;border-color:#198754}.btn-success:hover{color:#fff;background-color:#157347;border-color:#146c43}.btn-check:focus+.btn-success,.btn-success:focus{color:#fff;background-color:#157347;border-color:#146c43;box-shadow:0 0 0 .25rem rgba(60,153,110,.5)}.btn-check:active+.btn-success,.btn-check:checked+.btn-success,.btn-success.active,.btn-success:active,.show>.btn-success.dropdown-toggle{color:#fff;background-color:#146c43;border-color:#13653f}.btn-check:active+.btn-success:focus,.btn-check:checked+.btn-success:focus,.btn-success.active:focus,.btn-success:active:focus,.show>.btn-success.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(60,153,110,.5)}.btn-success.disabled,.btn-success:disabled{color:#fff;background-color:#198754;border-color:#198754}.btn-info{color:#000;background-color:#0dcaf0;border-color:#0dcaf0}.btn-info:hover{color:#000;background-color:#31d2f2;border-color:#25cff2}.btn-check:focus+.btn-info,.btn-info:focus{color:#000;background-color:#31d2f2;border-color:#25cff2;box-shadow:0 0 0 .25rem rgba(11,172,204,.5)}.btn-check:active+.btn-info,.btn-check:checked+.btn-info,.btn-info.active,.btn-info:active,.show>.btn-info.dropdown-toggle{color:#000;background-color:#3dd5f3;border-color:#25cff2}.btn-check:active+.btn-info:focus,.btn-check:checked+.btn-info:focus,.btn-info.active:focus,.btn-info:active:focus,.show>.btn-info.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(11,172,204,.5)}.btn-info.disabled,.btn-info:disabled{color:#000;background-color:#0dcaf0;border-color:#0dcaf0}.btn-warning{color:#000;background-color:#ffc107;border-color:#ffc107}.btn-warning:hover{color:#000;background-color:#ffca2c;border-color:#ffc720}.btn-check:focus+.btn-warning,.btn-warning:focus{color:#000;background-color:#ffca2c;border-color:#ffc720;box-shadow:0 0 0 .25rem rgba(217,164,6,.5)}.btn-check:active+.btn-warning,.btn-check:checked+.btn-warning,.btn-warning.active,.btn-warning:active,.show>.btn-warning.dropdown-toggle{color:#000;background-color:#ffcd39;border-color:#ffc720}.btn-check:active+.btn-warning:focus,.btn-check:checked+.btn-warning:focus,.btn-warning.active:focus,.btn-warning:active:focus,.show>.btn-warning.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(217,164,6,.5)}.btn-warning.disabled,.btn-warning:disabled{color:#000;background-color:#ffc107;border-color:#ffc107}.btn-danger{color:#fff;background-color:#dc3545;border-color:#dc3545}.btn-danger:hover{color:#fff;background-color:#bb2d3b;border-color:#b02a37}.btn-check:focus+.btn-danger,.btn-danger:focus{color:#fff;background-color:#bb2d3b;border-color:#b02a37;box-shadow:0 0 0 .25rem rgba(225,83,97,.5)}.btn-check:active+.btn-danger,.btn-check:checked+.btn-danger,.btn-danger.active,.btn-danger:active,.show>.btn-danger.dropdown-toggle{color:#fff;background-color:#b02a37;border-color:#a52834}.btn-check:active+.btn-danger:focus,.btn-check:checked+.btn-danger:focus,.btn-danger.active:focus,.btn-danger:active:focus,.show>.btn-danger.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(225,83,97,.5)}.btn-danger.disabled,.btn-danger:disabled{color:#fff;background-color:#dc3545;border-color:#dc3545}.btn-light{color:#000;background-color:#f8f9fa;border-color:#f8f9fa}.btn-light:hover{color:#000;background-color:#f9fafb;border-color:#f9fafb}.btn-check:focus+.btn-light,.btn-light:focus{color:#000;background-color:#f9fafb;border-color:#f9fafb;box-shadow:0 0 0 .25rem rgba(211,212,213,.5)}.btn-check:active+.btn-light,.btn-check:checked+.btn-light,.btn-light.active,.btn-light:active,.show>.btn-light.dropdown-toggle{color:#000;background-color:#f9fafb;border-color:#f9fafb}.btn-check:active+.btn-light:focus,.btn-check:checked+.btn-light:focus,.btn-light.active:focus,.btn-light:active:focus,.show>.btn-light.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(211,212,213,.5)}.btn-light.disabled,.btn-light:disabled{color:#000;background-color:#f8f9fa;border-color:#f8f9fa}.btn-dark{color:#fff;background-color:#212529;border-color:#212529}.btn-dark:hover{color:#fff;background-color:#1c1f23;border-color:#1a1e21}.btn-check:focus+.btn-dark,.btn-dark:focus{color:#fff;background-color:#1c1f23;border-color:#1a1e21;box-shadow:0 0 0 .25rem rgba(66,70,73,.5)}.btn-check:active+.btn-dark,.btn-check:checked+.btn-dark,.btn-dark.active,.btn-dark:active,.show>.btn-dark.dropdown-toggle{color:#fff;background-color:#1a1e21;border-color:#191c1f}.btn-check:active+.btn-dark:focus,.btn-check:checked+.btn-dark:focus,.btn-dark.active:focus,.btn-dark:active:focus,.show>.btn-dark.dropdown-toggle:focus{box-shadow:0 0 0 .25rem rgba(66,70,73,.5)}.btn-dark.disabled,.btn-dark:disabled{color:#fff;background-color:#212529;border-color:#212529}.btn-outline-primary{color:#0d6efd;border-color:#0d6efd}.btn-outline-primary:hover{color:#fff;background-color:#0d6efd;border-color:#0d6efd}.btn-check:focus+.btn-outline-primary,.btn-outline-primary:focus{box-shadow:0 0 0 .25rem rgba(13,110,253,.5)}.btn-check:active+.btn-outline-primary,.btn-check:checked+.btn-outline-primary,.btn-outline-primary.active,.btn-outline-primary.dropdown-toggle.show,.btn-outline-primary:active{color:#fff;background-color:#0d6efd;border-color:#0d6efd}.btn-check:active+.btn-outline-primary:focus,.btn-check:checked+.btn-outline-primary:focus,.btn-outline-primary.active:focus,.btn-outline-primary.dropdown-toggle.show:focus,.btn-outline-primary:active:focus{box-shadow:0 0 0 .25rem rgba(13,110,253,.5)}.btn-outline-primary.disabled,.btn-outline-primary:disabled{color:#0d6efd;background-color:transparent}.btn-outline-secondary{color:#6c757d;border-color:#6c757d}.btn-outline-secondary:hover{color:#fff;background-color:#6c757d;border-color:#6c757d}.btn-check:focus+.btn-outline-secondary,.btn-outline-secondary:focus{box-shadow:0 0 0 .25rem rgba(108,117,125,.5)}.btn-check:active+.btn-outline-secondary,.btn-check:checked+.btn-outline-secondary,.btn-outline-secondary.active,.btn-outline-secondary.dropdown-toggle.show,.btn-outline-secondary:active{color:#fff;background-color:#6c757d;border-color:#6c757d}.btn-check:active+.btn-outline-secondary:focus,.btn-check:checked+.btn-outline-secondary:focus,.btn-outline-secondary.active:focus,.btn-outline-secondary.dropdown-toggle.show:focus,.btn-outline-secondary:active:focus{box-shadow:0 0 0 .25rem rgba(108,117,125,.5)}.btn-outline-secondary.disabled,.btn-outline-secondary:disabled{color:#6c757d;background-color:transparent}.btn-outline-success{color:#198754;border-color:#198754}.btn-outline-success:hover{color:#fff;background-color:#198754;border-color:#198754}.btn-check:focus+.btn-outline-success,.btn-outline-success:focus{box-shadow:0 0 0 .25rem rgba(25,135,84,.5)}.btn-check:active+.btn-outline-success,.btn-check:checked+.btn-outline-success,.btn-outline-success.active,.btn-outline-success.dropdown-toggle.show,.btn-outline-success:active{color:#fff;background-color:#198754;border-color:#198754}.btn-check:active+.btn-outline-success:focus,.btn-check:checked+.btn-outline-success:focus,.btn-outline-success.active:focus,.btn-outline-success.dropdown-toggle.show:focus,.btn-outline-success:active:focus{box-shadow:0 0 0 .25rem rgba(25,135,84,.5)}.btn-outline-success.disabled,.btn-outline-success:disabled{color:#198754;background-color:transparent}.btn-outline-info{color:#0dcaf0;border-color:#0dcaf0}.btn-outline-info:hover{color:#000;background-color:#0dcaf0;border-color:#0dcaf0}.btn-check:focus+.btn-outline-info,.btn-outline-info:focus{box-shadow:0 0 0 .25rem rgba(13,202,240,.5)}.btn-check:active+.btn-outline-info,.btn-check:checked+.btn-outline-info,.btn-outline-info.active,.btn-outline-info.dropdown-toggle.show,.btn-outline-info:active{color:#000;background-color:#0dcaf0;border-color:#0dcaf0}.btn-check:active+.btn-outline-info:focus,.btn-check:checked+.btn-outline-info:focus,.btn-outline-info.active:focus,.btn-outline-info.dropdown-toggle.show:focus,.btn-outline-info:active:focus{box-shadow:0 0 0 .25rem rgba(13,202,240,.5)}.btn-outline-info.disabled,.btn-outline-info:disabled{color:#0dcaf0;background-color:transparent}.btn-outline-warning{color:#ffc107;border-color:#ffc107}.btn-outline-warning:hover{color:#000;background-color:#ffc107;border-color:#ffc107}.btn-check:focus+.btn-outline-warning,.btn-outline-warning:focus{box-shadow:0 0 0 .25rem rgba(255,193,7,.5)}.btn-check:active+.btn-outline-warning,.btn-check:checked+.btn-outline-warning,.btn-outline-warning.active,.btn-outline-warning.dropdown-toggle.show,.btn-outline-warning:active{color:#000;background-color:#ffc107;border-color:#ffc107}.btn-check:active+.btn-outline-warning:focus,.btn-check:checked+.btn-outline-warning:focus,.btn-outline-warning.active:focus,.btn-outline-warning.dropdown-toggle.show:focus,.btn-outline-warning:active:focus{box-shadow:0 0 0 .25rem rgba(255,193,7,.5)}.btn-outline-warning.disabled,.btn-outline-warning:disabled{color:#ffc107;background-color:transparent}.btn-outline-danger{color:#dc3545;border-color:#dc3545}.btn-outline-danger:hover{color:#fff;background-color:#dc3545;border-color:#dc3545}.btn-check:focus+.btn-outline-danger,.btn-outline-danger:focus{box-shadow:0 0 0 .25rem rgba(220,53,69,.5)}.btn-check:active+.btn-outline-danger,.btn-check:checked+.btn-outline-danger,.btn-outline-danger.active,.btn-outline-danger.dropdown-toggle.show,.btn-outline-danger:active{color:#fff;background-color:#dc3545;border-color:#dc3545}.btn-check:active+.btn-outline-danger:focus,.btn-check:checked+.btn-outline-danger:focus,.btn-outline-danger.active:focus,.btn-outline-danger.dropdown-toggle.show:focus,.btn-outline-danger:active:focus{box-shadow:0 0 0 .25rem rgba(220,53,69,.5)}.btn-outline-danger.disabled,.btn-outline-danger:disabled{color:#dc3545;background-color:transparent}.btn-outline-light{color:#f8f9fa;border-color:#f8f9fa}.btn-outline-light:hover{color:#000;background-color:#f8f9fa;border-color:#f8f9fa}.btn-check:focus+.btn-outline-light,.btn-outline-light:focus{box-shadow:0 0 0 .25rem rgba(248,249,250,.5)}.btn-check:active+.btn-outline-light,.btn-check:checked+.btn-outline-light,.btn-outline-light.active,.btn-outline-light.dropdown-toggle.show,.btn-outline-light:active{color:#000;background-color:#f8f9fa;border-color:#f8f9fa}.btn-check:active+.btn-outline-light:focus,.btn-check:checked+.btn-outline-light:focus,.btn-outline-light.active:focus,.btn-outline-light.dropdown-toggle.show:focus,.btn-outline-light:active:focus{box-shadow:0 0 0 .25rem rgba(248,249,250,.5)}.btn-outline-light.disabled,.btn-outline-light:disabled{color:#f8f9fa;background-color:transparent}.btn-outline-dark{color:#212529;border-color:#212529}.btn-outline-dark:hover{color:#fff;background-color:#212529;border-color:#212529}.btn-check:focus+.btn-outline-dark,.btn-outline-dark:focus{box-shadow:0 0 0 .25rem rgba(33,37,41,.5)}.btn-check:active+.btn-outline-dark,.btn-check:checked+.btn-outline-dark,.btn-outline-dark.active,.btn-outline-dark.dropdown-toggle.show,.btn-outline-dark:active{color:#fff;background-color:#212529;border-color:#212529}.btn-check:active+.btn-outline-dark:focus,.btn-check:checked+.btn-outline-dark:focus,.btn-outline-dark.active:focus,.btn-outline-dark.dropdown-toggle.show:focus,.btn-outline-dark:active:focus{box-shadow:0 0 0 .25rem rgba(33,37,41,.5)}.btn-outline-dark.disabled,.btn-outline-dark:disabled{color:#212529;background-color:transparent}.btn-link{font-weight:400;color:#0d6efd;text-decoration:underline}.btn-link:hover{color:#0a58ca}.btn-link.disabled,.btn-link:disabled{color:#6c757d}.btn-group-lg>.btn,.btn-lg{padding:.5rem 1rem;font-size:1.25rem;border-radius:.3rem}.btn-group-sm>.btn,.btn-sm{padding:.25rem .5rem;font-size:.875rem;border-radius:.2rem}.fade{transition:opacity .15s linear}@media (prefers-reduced-motion:reduce){.fade{transition:none}}.fade:not(.show){opacity:0}.collapse:not(.show){display:none}.collapsing{height:0;overflow:hidden;transition:height .35s ease}@media (prefers-reduced-motion:reduce){.collapsing{transition:none}}.collapsing.collapse-horizontal{width:0;height:auto;transition:width .35s ease}@media (prefers-reduced-motion:reduce){.collapsing.collapse-horizontal{transition:none}}.dropdown,.dropend,.dropstart,.dropup{position:relative}.dropdown-toggle{white-space:nowrap}.dropdown-toggle::after{display:inline-block;margin-left:.255em;vertical-align:.255em;content:"";border-top:.3em solid;border-right:.3em solid transparent;border-bottom:0;border-left:.3em solid transparent}.dropdown-toggle:empty::after{margin-left:0}.dropdown-menu{position:absolute;z-index:1000;display:none;min-width:10rem;padding:.5rem 0;margin:0;font-size:1rem;color:#212529;text-align:left;list-style:none;background-color:#fff;background-clip:padding-box;border:1px solid rgba(0,0,0,.15);border-radius:.25rem}.dropdown-menu[data-bs-popper]{top:100%;left:0;margin-top:.125rem}.dropdown-menu-start{--bs-position:start}.dropdown-menu-start[data-bs-popper]{right:auto;left:0}.dropdown-menu-end{--bs-position:end}.dropdown-menu-end[data-bs-popper]{right:0;left:auto}@media (min-width:576px){.dropdown-menu-sm-start{--bs-position:start}.dropdown-menu-sm-start[data-bs-popper]{right:auto;left:0}.dropdown-menu-sm-end{--bs-position:end}.dropdown-menu-sm-end[data-bs-popper]{right:0;left:auto}}@media (min-width:768px){.dropdown-menu-md-start{--bs-position:start}.dropdown-menu-md-start[data-bs-popper]{right:auto;left:0}.dropdown-menu-md-end{--bs-position:end}.dropdown-menu-md-end[data-bs-popper]{right:0;left:auto}}@media (min-width:992px){.dropdown-menu-lg-start{--bs-position:start}.dropdown-menu-lg-start[data-bs-popper]{right:auto;left:0}.dropdown-menu-lg-end{--bs-position:end}.dropdown-menu-lg-end[data-bs-popper]{right:0;left:auto}}@media (min-width:1200px){.dropdown-menu-xl-start{--bs-position:start}.dropdown-menu-xl-start[data-bs-popper]{right:auto;left:0}.dropdown-menu-xl-end{--bs-position:end}.dropdown-menu-xl-end[data-bs-popper]{right:0;left:auto}}@media (min-width:1400px){.dropdown-menu-xxl-start{--bs-position:start}.dropdown-menu-xxl-start[data-bs-popper]{right:auto;left:0}.dropdown-menu-xxl-end{--bs-position:end}.dropdown-menu-xxl-end[data-bs-popper]{right:0;left:auto}}.dropup .dropdown-menu[data-bs-popper]{top:auto;bottom:100%;margin-top:0;margin-bottom:.125rem}.dropup .dropdown-toggle::after{display:inline-block;margin-left:.255em;vertical-align:.255em;content:"";border-top:0;border-right:.3em solid transparent;border-bottom:.3em solid;border-left:.3em solid transparent}.dropup .dropdown-toggle:empty::after{margin-left:0}.dropend .dropdown-menu[data-bs-popper]{top:0;right:auto;left:100%;margin-top:0;margin-left:.125rem}.dropend .dropdown-toggle::after{display:inline-block;margin-left:.255em;vertical-align:.255em;content:"";border-top:.3em solid transparent;border-right:0;border-bottom:.3em solid transparent;border-left:.3em solid}.dropend .dropdown-toggle:empty::after{margin-left:0}.dropend .dropdown-toggle::after{vertical-align:0}.dropstart .dropdown-menu[data-bs-popper]{top:0;right:100%;left:auto;margin-top:0;margin-right:.125rem}.dropstart .dropdown-toggle::after{display:inline-block;margin-left:.255em;vertical-align:.255em;content:""}.dropstart .dropdown-toggle::after{display:none}.dropstart .dropdown-toggle::before{display:inline-block;margin-right:.255em;vertical-align:.255em;content:"";border-top:.3em solid transparent;border-right:.3em solid;border-bottom:.3em solid transparent}.dropstart .dropdown-toggle:empty::after{margin-left:0}.dropstart .dropdown-toggle::before{vertical-align:0}.dropdown-divider{height:0;margin:.5rem 0;overflow:hidden;border-top:1px solid rgba(0,0,0,.15)}.dropdown-item{display:block;width:100%;padding:.25rem 1rem;clear:both;font-weight:400;color:#212529;text-align:inherit;text-decoration:none;white-space:nowrap;background-color:transparent;border:0}.dropdown-item:focus,.dropdown-item:hover{color:#1e2125;background-color:#e9ecef}.dropdown-item.active,.dropdown-item:active{color:#fff;text-decoration:none;background-color:#0d6efd}.dropdown-item.disabled,.dropdown-item:disabled{color:#adb5bd;pointer-events:none;background-color:transparent}.dropdown-menu.show{display:block}.dropdown-header{display:block;padding:.5rem 1rem;margin-bottom:0;font-size:.875rem;color:#6c757d;white-space:nowrap}.dropdown-item-text{display:block;padding:.25rem 1rem;color:#212529}.dropdown-menu-dark{color:#dee2e6;background-color:#343a40;border-color:rgba(0,0,0,.15)}.dropdown-menu-dark .dropdown-item{color:#dee2e6}.dropdown-menu-dark .dropdown-item:focus,.dropdown-menu-dark .dropdown-item:hover{color:#fff;background-color:rgba(255,255,255,.15)}.dropdown-menu-dark .dropdown-item.active,.dropdown-menu-dark .dropdown-item:active{color:#fff;background-color:#0d6efd}.dropdown-menu-dark .dropdown-item.disabled,.dropdown-menu-dark .dropdown-item:disabled{color:#adb5bd}.dropdown-menu-dark .dropdown-divider{border-color:rgba(0,0,0,.15)}.dropdown-menu-dark .dropdown-item-text{color:#dee2e6}.dropdown-menu-dark .dropdown-header{color:#adb5bd}.btn-group,.btn-group-vertical{position:relative;display:inline-flex;vertical-align:middle}.btn-group-vertical>.btn,.btn-group>.btn{position:relative;flex:1 1 auto}.btn-group-vertical>.btn-check:checked+.btn,.btn-group-vertical>.btn-check:focus+.btn,.btn-group-vertical>.btn.active,.btn-group-vertical>.btn:active,.btn-group-vertical>.btn:focus,.btn-group-vertical>.btn:hover,.btn-group>.btn-check:checked+.btn,.btn-group>.btn-check:focus+.btn,.btn-group>.btn.active,.btn-group>.btn:active,.btn-group>.btn:focus,.btn-group>.btn:hover{z-index:1}.btn-toolbar{display:flex;flex-wrap:wrap;justify-content:flex-start}.btn-toolbar .input-group{width:auto}.btn-group>.btn-group:not(:first-child),.btn-group>.btn:not(:first-child){margin-left:-1px}.btn-group>.btn-group:not(:last-child)>.btn,.btn-group>.btn:not(:last-child):not(.dropdown-toggle){border-top-right-radius:0;border-bottom-right-radius:0}.btn-group>.btn-group:not(:first-child)>.btn,.btn-group>.btn:nth-child(n+3),.btn-group>:not(.btn-check)+.btn{border-top-left-radius:0;border-bottom-left-radius:0}.dropdown-toggle-split{padding-right:.5625rem;padding-left:.5625rem}.dropdown-toggle-split::after,.dropend .dropdown-toggle-split::after,.dropup .dropdown-toggle-split::after{margin-left:0}.dropstart .dropdown-toggle-split::before{margin-right:0}.btn-group-sm>.btn+.dropdown-toggle-split,.btn-sm+.dropdown-toggle-split{padding-right:.375rem;padding-left:.375rem}.btn-group-lg>.btn+.dropdown-toggle-split,.btn-lg+.dropdown-toggle-split{padding-right:.75rem;padding-left:.75rem}.btn-group-vertical{flex-direction:column;align-items:flex-start;justify-content:center}.btn-group-vertical>.btn,.btn-group-vertical>.btn-group{width:100%}.btn-group-vertical>.btn-group:not(:first-child),.btn-group-vertical>.btn:not(:first-child){margin-top:-1px}.btn-group-vertical>.btn-group:not(:last-child)>.btn,.btn-group-vertical>.btn:not(:last-child):not(.dropdown-toggle){border-bottom-right-radius:0;border-bottom-left-radius:0}.btn-group-vertical>.btn-group:not(:first-child)>.btn,.btn-group-vertical>.btn~.btn{border-top-left-radius:0;border-top-right-radius:0}.nav{display:flex;flex-wrap:wrap;padding-left:0;margin-bottom:0;list-style:none}.nav-link{display:block;padding:.5rem 1rem;color:#0d6efd;text-decoration:none;transition:color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out}@media (prefers-reduced-motion:reduce){.nav-link{transition:none}}.nav-link:focus,.nav-link:hover{color:#0a58ca}.nav-link.disabled{color:#6c757d;pointer-events:none;cursor:default}.nav-tabs{border-bottom:1px solid #dee2e6}.nav-tabs .nav-link{margin-bottom:-1px;background:0 0;border:1px solid transparent;border-top-left-radius:.25rem;border-top-right-radius:.25rem}.nav-tabs .nav-link:focus,.nav-tabs .nav-link:hover{border-color:#e9ecef #e9ecef #dee2e6;isolation:isolate}.nav-tabs .nav-link.disabled{color:#6c757d;background-color:transparent;border-color:transparent}.nav-tabs .nav-item.show .nav-link,.nav-tabs .nav-link.active{color:#495057;background-color:#fff;border-color:#dee2e6 #dee2e6 #fff}.nav-tabs .dropdown-menu{margin-top:-1px;border-top-left-radius:0;border-top-right-radius:0}.nav-pills .nav-link{background:0 0;border:0;border-radius:.25rem}.nav-pills .nav-link.active,.nav-pills .show>.nav-link{color:#fff;background-color:#0d6efd}.nav-fill .nav-item,.nav-fill>.nav-link{flex:1 1 auto;text-align:center}.nav-justified .nav-item,.nav-justified>.nav-link{flex-basis:0;flex-grow:1;text-align:center}.nav-fill .nav-item .nav-link,.nav-justified .nav-item .nav-link{width:100%}.tab-content>.tab-pane{display:none}.tab-content>.active{display:block}.navbar{position:relative;display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;padding-top:.5rem;padding-bottom:.5rem}.navbar>.container,.navbar>.container-fluid,.navbar>.container-lg,.navbar>.container-md,.navbar>.container-sm,.navbar>.container-xl,.navbar>.container-xxl{display:flex;flex-wrap:inherit;align-items:center;justify-content:space-between}.navbar-brand{padding-top:.3125rem;padding-bottom:.3125rem;margin-right:1rem;font-size:1.25rem;text-decoration:none;white-space:nowrap}.navbar-nav{display:flex;flex-direction:column;padding-left:0;margin-bottom:0;list-style:none}.navbar-nav .nav-link{padding-right:0;padding-left:0}.navbar-nav .dropdown-menu{position:static}.navbar-text{padding-top:.5rem;padding-bottom:.5rem}.navbar-collapse{flex-basis:100%;flex-grow:1;align-items:center}.navbar-toggler{padding:.25rem .75rem;font-size:1.25rem;line-height:1;background-color:transparent;border:1px solid transparent;border-radius:.25rem;transition:box-shadow .15s ease-in-out}@media (prefers-reduced-motion:reduce){.navbar-toggler{transition:none}}.navbar-toggler:hover{text-decoration:none}.navbar-toggler:focus{text-decoration:none;outline:0;box-shadow:0 0 0 .25rem}.navbar-toggler-icon{display:inline-block;width:1.5em;height:1.5em;vertical-align:middle;background-repeat:no-repeat;background-position:center;background-size:100%}.navbar-nav-scroll{max-height:var(--bs-scroll-height,75vh);overflow-y:auto}@media (min-width:576px){.navbar-expand-sm{flex-wrap:nowrap;justify-content:flex-start}.navbar-expand-sm .navbar-nav{flex-direction:row}.navbar-expand-sm .navbar-nav .dropdown-menu{position:absolute}.navbar-expand-sm .navbar-nav .nav-link{padding-right:.5rem;padding-left:.5rem}.navbar-expand-sm .navbar-nav-scroll{overflow:visible}.navbar-expand-sm .navbar-collapse{display:flex!important;flex-basis:auto}.navbar-expand-sm .navbar-toggler{display:none}.navbar-expand-sm .offcanvas-header{display:none}.navbar-expand-sm .offcanvas{position:inherit;bottom:0;z-index:1000;flex-grow:1;visibility:visible!important;background-color:transparent;border-right:0;border-left:0;transition:none;transform:none}.navbar-expand-sm .offcanvas-bottom,.navbar-expand-sm .offcanvas-top{height:auto;border-top:0;border-bottom:0}.navbar-expand-sm .offcanvas-body{display:flex;flex-grow:0;padding:0;overflow-y:visible}}@media (min-width:768px){.navbar-expand-md{flex-wrap:nowrap;justify-content:flex-start}.navbar-expand-md .navbar-nav{flex-direction:row}.navbar-expand-md .navbar-nav .dropdown-menu{position:absolute}.navbar-expand-md .navbar-nav .nav-link{padding-right:.5rem;padding-left:.5rem}.navbar-expand-md .navbar-nav-scroll{overflow:visible}.navbar-expand-md .navbar-collapse{display:flex!important;flex-basis:auto}.navbar-expand-md .navbar-toggler{display:none}.navbar-expand-md .offcanvas-header{display:none}.navbar-expand-md .offcanvas{position:inherit;bottom:0;z-index:1000;flex-grow:1;visibility:visible!important;background-color:transparent;border-right:0;border-left:0;transition:none;transform:none}.navbar-expand-md .offcanvas-bottom,.navbar-expand-md .offcanvas-top{height:auto;border-top:0;border-bottom:0}.navbar-expand-md .offcanvas-body{display:flex;flex-grow:0;padding:0;overflow-y:visible}}@media (min-width:992px){.navbar-expand-lg{flex-wrap:nowrap;justify-content:flex-start}.navbar-expand-lg .navbar-nav{flex-direction:row}.navbar-expand-lg .navbar-nav .dropdown-menu{position:absolute}.navbar-expand-lg .navbar-nav .nav-link{padding-right:.5rem;padding-left:.5rem}.navbar-expand-lg .navbar-nav-scroll{overflow:visible}.navbar-expand-lg .navbar-collapse{display:flex!important;flex-basis:auto}.navbar-expand-lg .navbar-toggler{display:none}.navbar-expand-lg .offcanvas-header{display:none}.navbar-expand-lg .offcanvas{position:inherit;bottom:0;z-index:1000;flex-grow:1;visibility:visible!important;background-color:transparent;border-right:0;border-left:0;transition:none;transform:none}.navbar-expand-lg .offcanvas-bottom,.navbar-expand-lg .offcanvas-top{height:auto;border-top:0;border-bottom:0}.navbar-expand-lg .offcanvas-body{display:flex;flex-grow:0;padding:0;overflow-y:visible}}@media (min-width:1200px){.navbar-expand-xl{flex-wrap:nowrap;justify-content:flex-start}.navbar-expand-xl .navbar-nav{flex-direction:row}.navbar-expand-xl .navbar-nav .dropdown-menu{position:absolute}.navbar-expand-xl .navbar-nav .nav-link{padding-right:.5rem;padding-left:.5rem}.navbar-expand-xl .navbar-nav-scroll{overflow:visible}.navbar-expand-xl .navbar-collapse{display:flex!important;flex-basis:auto}.navbar-expand-xl .navbar-toggler{display:none}.navbar-expand-xl .offcanvas-header{display:none}.navbar-expand-xl .offcanvas{position:inherit;bottom:0;z-index:1000;flex-grow:1;visibility:visible!important;background-color:transparent;border-right:0;border-left:0;transition:none;transform:none}.navbar-expand-xl .offcanvas-bottom,.navbar-expand-xl .offcanvas-top{height:auto;border-top:0;border-bottom:0}.navbar-expand-xl .offcanvas-body{display:flex;flex-grow:0;padding:0;overflow-y:visible}}@media (min-width:1400px){.navbar-expand-xxl{flex-wrap:nowrap;justify-content:flex-start}.navbar-expand-xxl .navbar-nav{flex-direction:row}.navbar-expand-xxl .navbar-nav .dropdown-menu{position:absolute}.navbar-expand-xxl .navbar-nav .nav-link{padding-right:.5rem;padding-left:.5rem}.navbar-expand-xxl .navbar-nav-scroll{overflow:visible}.navbar-expand-xxl .navbar-collapse{display:flex!important;flex-basis:auto}.navbar-expand-xxl .navbar-toggler{display:none}.navbar-expand-xxl .offcanvas-header{display:none}.navbar-expand-xxl .offcanvas{position:inherit;bottom:0;z-index:1000;flex-grow:1;visibility:visible!important;background-color:transparent;border-right:0;border-left:0;transition:none;transform:none}.navbar-expand-xxl .offcanvas-bottom,.navbar-expand-xxl .offcanvas-top{height:auto;border-top:0;border-bottom:0}.navbar-expand-xxl .offcanvas-body{display:flex;flex-grow:0;padding:0;overflow-y:visible}}.navbar-expand{flex-wrap:nowrap;justify-content:flex-start}.navbar-expand .navbar-nav{flex-direction:row}.navbar-expand .navbar-nav .dropdown-menu{position:absolute}.navbar-expand .navbar-nav .nav-link{padding-right:.5rem;padding-left:.5rem}.navbar-expand .navbar-nav-scroll{overflow:visible}.navbar-expand .navbar-collapse{display:flex!important;flex-basis:auto}.navbar-expand .navbar-toggler{display:none}.navbar-expand .offcanvas-header{display:none}.navbar-expand .offcanvas{position:inherit;bottom:0;z-index:1000;flex-grow:1;visibility:visible!important;background-color:transparent;border-right:0;border-left:0;transition:none;transform:none}.navbar-expand .offcanvas-bottom,.navbar-expand .offcanvas-top{height:auto;border-top:0;border-bottom:0}.navbar-expand .offcanvas-body{display:flex;flex-grow:0;padding:0;overflow-y:visible}.navbar-light .navbar-brand{color:rgba(0,0,0,.9)}.navbar-light .navbar-brand:focus,.navbar-light .navbar-brand:hover{color:rgba(0,0,0,.9)}.navbar-light .navbar-nav .nav-link{color:rgba(0,0,0,.55)}.navbar-light .navbar-nav .nav-link:focus,.navbar-light .navbar-nav .nav-link:hover{color:rgba(0,0,0,.7)}.navbar-light .navbar-nav .nav-link.disabled{color:rgba(0,0,0,.3)}.navbar-light .navbar-nav .nav-link.active,.navbar-light .navbar-nav .show>.nav-link{color:rgba(0,0,0,.9)}.navbar-light .navbar-toggler{color:rgba(0,0,0,.55);border-color:rgba(0,0,0,.1)}.navbar-light .navbar-toggler-icon{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 30 30'%3e%3cpath stroke='rgba%280, 0, 0, 0.55%29' stroke-linecap='round' stroke-miterlimit='10' stroke-width='2' d='M4 7h22M4 15h22M4 23h22'/%3e%3c/svg%3e")}.navbar-light .navbar-text{color:rgba(0,0,0,.55)}.navbar-light .navbar-text a,.navbar-light .navbar-text a:focus,.navbar-light .navbar-text a:hover{color:rgba(0,0,0,.9)}.navbar-dark .navbar-brand{color:#fff}.navbar-dark .navbar-brand:focus,.navbar-dark .navbar-brand:hover{color:#fff}.navbar-dark .navbar-nav .nav-link{color:rgba(255,255,255,.55)}.navbar-dark .navbar-nav .nav-link:focus,.navbar-dark .navbar-nav .nav-link:hover{color:rgba(255,255,255,.75)}.navbar-dark .navbar-nav .nav-link.disabled{color:rgba(255,255,255,.25)}.navbar-dark .navbar-nav .nav-link.active,.navbar-dark .navbar-nav .show>.nav-link{color:#fff}.navbar-dark .navbar-toggler{color:rgba(255,255,255,.55);border-color:rgba(255,255,255,.1)}.navbar-dark .navbar-toggler-icon{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 30 30'%3e%3cpath stroke='rgba%28255, 255, 255, 0.55%29' stroke-linecap='round' stroke-miterlimit='10' stroke-width='2' d='M4 7h22M4 15h22M4 23h22'/%3e%3c/svg%3e")}.navbar-dark .navbar-text{color:rgba(255,255,255,.55)}.navbar-dark .navbar-text a,.navbar-dark .navbar-text a:focus,.navbar-dark .navbar-text a:hover{color:#fff}.card{position:relative;display:flex;flex-direction:column;min-width:0;word-wrap:break-word;background-color:#fff;background-clip:border-box;border:1px solid rgba(0,0,0,.125);border-radius:.25rem}.card>hr{margin-right:0;margin-left:0}.card>.list-group{border-top:inherit;border-bottom:inherit}.card>.list-group:first-child{border-top-width:0;border-top-left-radius:calc(.25rem - 1px);border-top-right-radius:calc(.25rem - 1px)}.card>.list-group:last-child{border-bottom-width:0;border-bottom-right-radius:calc(.25rem - 1px);border-bottom-left-radius:calc(.25rem - 1px)}.card>.card-header+.list-group,.card>.list-group+.card-footer{border-top:0}.card-body{flex:1 1 auto;padding:1rem 1rem}.card-title{margin-bottom:.5rem}.card-subtitle{margin-top:-.25rem;margin-bottom:0}.card-text:last-child{margin-bottom:0}.card-link+.card-link{margin-left:1rem}.card-header{padding:.5rem 1rem;margin-bottom:0;background-color:rgba(0,0,0,.03);border-bottom:1px solid rgba(0,0,0,.125)}.card-header:first-child{border-radius:calc(.25rem - 1px) calc(.25rem - 1px) 0 0}.card-footer{padding:.5rem 1rem;background-color:rgba(0,0,0,.03);border-top:1px solid rgba(0,0,0,.125)}.card-footer:last-child{border-radius:0 0 calc(.25rem - 1px) calc(.25rem - 1px)}.card-header-tabs{margin-right:-.5rem;margin-bottom:-.5rem;margin-left:-.5rem;border-bottom:0}.card-header-pills{margin-right:-.5rem;margin-left:-.5rem}.card-img-overlay{position:absolute;top:0;right:0;bottom:0;left:0;padding:1rem;border-radius:calc(.25rem - 1px)}.card-img,.card-img-bottom,.card-img-top{width:100%}.card-img,.card-img-top{border-top-left-radius:calc(.25rem - 1px);border-top-right-radius:calc(.25rem - 1px)}.card-img,.card-img-bottom{border-bottom-right-radius:calc(.25rem - 1px);border-bottom-left-radius:calc(.25rem - 1px)}.card-group>.card{margin-bottom:.75rem}@media (min-width:576px){.card-group{display:flex;flex-flow:row wrap}.card-group>.card{flex:1 0 0%;margin-bottom:0}.card-group>.card+.card{margin-left:0;border-left:0}.card-group>.card:not(:last-child){border-top-right-radius:0;border-bottom-right-radius:0}.card-group>.card:not(:last-child) .card-header,.card-group>.card:not(:last-child) .card-img-top{border-top-right-radius:0}.card-group>.card:not(:last-child) .card-footer,.card-group>.card:not(:last-child) .card-img-bottom{border-bottom-right-radius:0}.card-group>.card:not(:first-child){border-top-left-radius:0;border-bottom-left-radius:0}.card-group>.card:not(:first-child) .card-header,.card-group>.card:not(:first-child) .card-img-top{border-top-left-radius:0}.card-group>.card:not(:first-child) .card-footer,.card-group>.card:not(:first-child) .card-img-bottom{border-bottom-left-radius:0}}.accordion-button{position:relative;display:flex;align-items:center;width:100%;padding:1rem 1.25rem;font-size:1rem;color:#212529;text-align:left;background-color:#fff;border:0;border-radius:0;overflow-anchor:none;transition:color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out,border-radius .15s ease}@media (prefers-reduced-motion:reduce){.accordion-button{transition:none}}.accordion-button:not(.collapsed){color:#0c63e4;background-color:#e7f1ff;box-shadow:inset 0 -1px 0 rgba(0,0,0,.125)}.accordion-button:not(.collapsed)::after{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16' fill='%230c63e4'%3e%3cpath fill-rule='evenodd' d='M1.646 4.646a.5.5 0 0 1 .708 0L8 10.293l5.646-5.647a.5.5 0 0 1 .708.708l-6 6a.5.5 0 0 1-.708 0l-6-6a.5.5 0 0 1 0-.708z'/%3e%3c/svg%3e");transform:rotate(-180deg)}.accordion-button::after{flex-shrink:0;width:1.25rem;height:1.25rem;margin-left:auto;content:"";background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16' fill='%23212529'%3e%3cpath fill-rule='evenodd' d='M1.646 4.646a.5.5 0 0 1 .708 0L8 10.293l5.646-5.647a.5.5 0 0 1 .708.708l-6 6a.5.5 0 0 1-.708 0l-6-6a.5.5 0 0 1 0-.708z'/%3e%3c/svg%3e");background-repeat:no-repeat;background-size:1.25rem;transition:transform .2s ease-in-out}@media (prefers-reduced-motion:reduce){.accordion-button::after{transition:none}}.accordion-button:hover{z-index:2}.accordion-button:focus{z-index:3;border-color:#86b7fe;outline:0;box-shadow:0 0 0 .25rem rgba(13,110,253,.25)}.accordion-header{margin-bottom:0}.accordion-item{background-color:#fff;border:1px solid rgba(0,0,0,.125)}.accordion-item:first-of-type{border-top-left-radius:.25rem;border-top-right-radius:.25rem}.accordion-item:first-of-type .accordion-button{border-top-left-radius:calc(.25rem - 1px);border-top-right-radius:calc(.25rem - 1px)}.accordion-item:not(:first-of-type){border-top:0}.accordion-item:last-of-type{border-bottom-right-radius:.25rem;border-bottom-left-radius:.25rem}.accordion-item:last-of-type .accordion-button.collapsed{border-bottom-right-radius:calc(.25rem - 1px);border-bottom-left-radius:calc(.25rem - 1px)}.accordion-item:last-of-type .accordion-collapse{border-bottom-right-radius:.25rem;border-bottom-left-radius:.25rem}.accordion-body{padding:1rem 1.25rem}.accordion-flush .accordion-collapse{border-width:0}.accordion-flush .accordion-item{border-right:0;border-left:0;border-radius:0}.accordion-flush .accordion-item:first-child{border-top:0}.accordion-flush .accordion-item:last-child{border-bottom:0}.accordion-flush .accordion-item .accordion-button{border-radius:0}.breadcrumb{display:flex;flex-wrap:wrap;padding:0 0;margin-bottom:1rem;list-style:none}.breadcrumb-item+.breadcrumb-item{padding-left:.5rem}.breadcrumb-item+.breadcrumb-item::before{float:left;padding-right:.5rem;color:#6c757d;content:var(--bs-breadcrumb-divider, "/")}.breadcrumb-item.active{color:#6c757d}.pagination{display:flex;padding-left:0;list-style:none}.page-link{position:relative;display:block;color:#0d6efd;text-decoration:none;background-color:#fff;border:1px solid #dee2e6;transition:color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out}@media (prefers-reduced-motion:reduce){.page-link{transition:none}}.page-link:hover{z-index:2;color:#0a58ca;background-color:#e9ecef;border-color:#dee2e6}.page-link:focus{z-index:3;color:#0a58ca;background-color:#e9ecef;outline:0;box-shadow:0 0 0 .25rem rgba(13,110,253,.25)}.page-item:not(:first-child) .page-link{margin-left:-1px}.page-item.active .page-link{z-index:3;color:#fff;background-color:#0d6efd;border-color:#0d6efd}.page-item.disabled .page-link{color:#6c757d;pointer-events:none;background-color:#fff;border-color:#dee2e6}.page-link{padding:.375rem .75rem}.page-item:first-child .page-link{border-top-left-radius:.25rem;border-bottom-left-radius:.25rem}.page-item:last-child .page-link{border-top-right-radius:.25rem;border-bottom-right-radius:.25rem}.pagination-lg .page-link{padding:.75rem 1.5rem;font-size:1.25rem}.pagination-lg .page-item:first-child .page-link{border-top-left-radius:.3rem;border-bottom-left-radius:.3rem}.pagination-lg .page-item:last-child .page-link{border-top-right-radius:.3rem;border-bottom-right-radius:.3rem}.pagination-sm .page-link{padding:.25rem .5rem;font-size:.875rem}.pagination-sm .page-item:first-child .page-link{border-top-left-radius:.2rem;border-bottom-left-radius:.2rem}.pagination-sm .page-item:last-child .page-link{border-top-right-radius:.2rem;border-bottom-right-radius:.2rem}.badge{display:inline-block;padding:.35em .65em;font-size:.75em;font-weight:700;line-height:1;color:#fff;text-align:center;white-space:nowrap;vertical-align:baseline;border-radius:.25rem}.badge:empty{display:none}.btn .badge{position:relative;top:-1px}.alert{position:relative;padding:1rem 1rem;margin-bottom:1rem;border:1px solid transparent;border-radius:.25rem}.alert-heading{color:inherit}.alert-link{font-weight:700}.alert-dismissible{padding-right:3rem}.alert-dismissible .btn-close{position:absolute;top:0;right:0;z-index:2;padding:1.25rem 1rem}.alert-primary{color:#084298;background-color:#cfe2ff;border-color:#b6d4fe}.alert-primary .alert-link{color:#06357a}.alert-secondary{color:#41464b;background-color:#e2e3e5;border-color:#d3d6d8}.alert-secondary .alert-link{color:#34383c}.alert-success{color:#0f5132;background-color:#d1e7dd;border-color:#badbcc}.alert-success .alert-link{color:#0c4128}.alert-info{color:#055160;background-color:#cff4fc;border-color:#b6effb}.alert-info .alert-link{color:#04414d}.alert-warning{color:#664d03;background-color:#fff3cd;border-color:#ffecb5}.alert-warning .alert-link{color:#523e02}.alert-danger{color:#842029;background-color:#f8d7da;border-color:#f5c2c7}.alert-danger .alert-link{color:#6a1a21}.alert-light{color:#636464;background-color:#fefefe;border-color:#fdfdfe}.alert-light .alert-link{color:#4f5050}.alert-dark{color:#141619;background-color:#d3d3d4;border-color:#bcbebf}.alert-dark .alert-link{color:#101214}@-webkit-keyframes progress-bar-stripes{0%{background-position-x:1rem}}@keyframes progress-bar-stripes{0%{background-position-x:1rem}}.progress{display:flex;height:1rem;overflow:hidden;font-size:.75rem;background-color:#e9ecef;border-radius:.25rem}.progress-bar{display:flex;flex-direction:column;justify-content:center;overflow:hidden;color:#fff;text-align:center;white-space:nowrap;background-color:#0d6efd;transition:width .6s ease}@media (prefers-reduced-motion:reduce){.progress-bar{transition:none}}.progress-bar-striped{background-image:linear-gradient(45deg,rgba(255,255,255,.15) 25%,transparent 25%,transparent 50%,rgba(255,255,255,.15) 50%,rgba(255,255,255,.15) 75%,transparent 75%,transparent);background-size:1rem 1rem}.progress-bar-animated{-webkit-animation:1s linear infinite progress-bar-stripes;animation:1s linear infinite progress-bar-stripes}@media (prefers-reduced-motion:reduce){.progress-bar-animated{-webkit-animation:none;animation:none}}.list-group{display:flex;flex-direction:column;padding-left:0;margin-bottom:0;border-radius:.25rem}.list-group-numbered{list-style-type:none;counter-reset:section}.list-group-numbered>li::before{content:counters(section, ".") ". ";counter-increment:section}.list-group-item-action{width:100%;color:#495057;text-align:inherit}.list-group-item-action:focus,.list-group-item-action:hover{z-index:1;color:#495057;text-decoration:none;background-color:#f8f9fa}.list-group-item-action:active{color:#212529;background-color:#e9ecef}.list-group-item{position:relative;display:block;padding:.5rem 1rem;color:#212529;text-decoration:none;background-color:#fff;border:1px solid rgba(0,0,0,.125)}.list-group-item:first-child{border-top-left-radius:inherit;border-top-right-radius:inherit}.list-group-item:last-child{border-bottom-right-radius:inherit;border-bottom-left-radius:inherit}.list-group-item.disabled,.list-group-item:disabled{color:#6c757d;pointer-events:none;background-color:#fff}.list-group-item.active{z-index:2;color:#fff;background-color:#0d6efd;border-color:#0d6efd}.list-group-item+.list-group-item{border-top-width:0}.list-group-item+.list-group-item.active{margin-top:-1px;border-top-width:1px}.list-group-horizontal{flex-direction:row}.list-group-horizontal>.list-group-item:first-child{border-bottom-left-radius:.25rem;border-top-right-radius:0}.list-group-horizontal>.list-group-item:last-child{border-top-right-radius:.25rem;border-bottom-left-radius:0}.list-group-horizontal>.list-group-item.active{margin-top:0}.list-group-horizontal>.list-group-item+.list-group-item{border-top-width:1px;border-left-width:0}.list-group-horizontal>.list-group-item+.list-group-item.active{margin-left:-1px;border-left-width:1px}@media (min-width:576px){.list-group-horizontal-sm{flex-direction:row}.list-group-horizontal-sm>.list-group-item:first-child{border-bottom-left-radius:.25rem;border-top-right-radius:0}.list-group-horizontal-sm>.list-group-item:last-child{border-top-right-radius:.25rem;border-bottom-left-radius:0}.list-group-horizontal-sm>.list-group-item.active{margin-top:0}.list-group-horizontal-sm>.list-group-item+.list-group-item{border-top-width:1px;border-left-width:0}.list-group-horizontal-sm>.list-group-item+.list-group-item.active{margin-left:-1px;border-left-width:1px}}@media (min-width:768px){.list-group-horizontal-md{flex-direction:row}.list-group-horizontal-md>.list-group-item:first-child{border-bottom-left-radius:.25rem;border-top-right-radius:0}.list-group-horizontal-md>.list-group-item:last-child{border-top-right-radius:.25rem;border-bottom-left-radius:0}.list-group-horizontal-md>.list-group-item.active{margin-top:0}.list-group-horizontal-md>.list-group-item+.list-group-item{border-top-width:1px;border-left-width:0}.list-group-horizontal-md>.list-group-item+.list-group-item.active{margin-left:-1px;border-left-width:1px}}@media (min-width:992px){.list-group-horizontal-lg{flex-direction:row}.list-group-horizontal-lg>.list-group-item:first-child{border-bottom-left-radius:.25rem;border-top-right-radius:0}.list-group-horizontal-lg>.list-group-item:last-child{border-top-right-radius:.25rem;border-bottom-left-radius:0}.list-group-horizontal-lg>.list-group-item.active{margin-top:0}.list-group-horizontal-lg>.list-group-item+.list-group-item{border-top-width:1px;border-left-width:0}.list-group-horizontal-lg>.list-group-item+.list-group-item.active{margin-left:-1px;border-left-width:1px}}@media (min-width:1200px){.list-group-horizontal-xl{flex-direction:row}.list-group-horizontal-xl>.list-group-item:first-child{border-bottom-left-radius:.25rem;border-top-right-radius:0}.list-group-horizontal-xl>.list-group-item:last-child{border-top-right-radius:.25rem;border-bottom-left-radius:0}.list-group-horizontal-xl>.list-group-item.active{margin-top:0}.list-group-horizontal-xl>.list-group-item+.list-group-item{border-top-width:1px;border-left-width:0}.list-group-horizontal-xl>.list-group-item+.list-group-item.active{margin-left:-1px;border-left-width:1px}}@media (min-width:1400px){.list-group-horizontal-xxl{flex-direction:row}.list-group-horizontal-xxl>.list-group-item:first-child{border-bottom-left-radius:.25rem;border-top-right-radius:0}.list-group-horizontal-xxl>.list-group-item:last-child{border-top-right-radius:.25rem;border-bottom-left-radius:0}.list-group-horizontal-xxl>.list-group-item.active{margin-top:0}.list-group-horizontal-xxl>.list-group-item+.list-group-item{border-top-width:1px;border-left-width:0}.list-group-horizontal-xxl>.list-group-item+.list-group-item.active{margin-left:-1px;border-left-width:1px}}.list-group-flush{border-radius:0}.list-group-flush>.list-group-item{border-width:0 0 1px}.list-group-flush>.list-group-item:last-child{border-bottom-width:0}.list-group-item-primary{color:#084298;background-color:#cfe2ff}.list-group-item-primary.list-group-item-action:focus,.list-group-item-primary.list-group-item-action:hover{color:#084298;background-color:#bacbe6}.list-group-item-primary.list-group-item-action.active{color:#fff;background-color:#084298;border-color:#084298}.list-group-item-secondary{color:#41464b;background-color:#e2e3e5}.list-group-item-secondary.list-group-item-action:focus,.list-group-item-secondary.list-group-item-action:hover{color:#41464b;background-color:#cbccce}.list-group-item-secondary.list-group-item-action.active{color:#fff;background-color:#41464b;border-color:#41464b}.list-group-item-success{color:#0f5132;background-color:#d1e7dd}.list-group-item-success.list-group-item-action:focus,.list-group-item-success.list-group-item-action:hover{color:#0f5132;background-color:#bcd0c7}.list-group-item-success.list-group-item-action.active{color:#fff;background-color:#0f5132;border-color:#0f5132}.list-group-item-info{color:#055160;background-color:#cff4fc}.list-group-item-info.list-group-item-action:focus,.list-group-item-info.list-group-item-action:hover{color:#055160;background-color:#badce3}.list-group-item-info.list-group-item-action.active{color:#fff;background-color:#055160;border-color:#055160}.list-group-item-warning{color:#664d03;background-color:#fff3cd}.list-group-item-warning.list-group-item-action:focus,.list-group-item-warning.list-group-item-action:hover{color:#664d03;background-color:#e6dbb9}.list-group-item-warning.list-group-item-action.active{color:#fff;background-color:#664d03;border-color:#664d03}.list-group-item-danger{color:#842029;background-color:#f8d7da}.list-group-item-danger.list-group-item-action:focus,.list-group-item-danger.list-group-item-action:hover{color:#842029;background-color:#dfc2c4}.list-group-item-danger.list-group-item-action.active{color:#fff;background-color:#842029;border-color:#842029}.list-group-item-light{color:#636464;background-color:#fefefe}.list-group-item-light.list-group-item-action:focus,.list-group-item-light.list-group-item-action:hover{color:#636464;background-color:#e5e5e5}.list-group-item-light.list-group-item-action.active{color:#fff;background-color:#636464;border-color:#636464}.list-group-item-dark{color:#141619;background-color:#d3d3d4}.list-group-item-dark.list-group-item-action:focus,.list-group-item-dark.list-group-item-action:hover{color:#141619;background-color:#bebebf}.list-group-item-dark.list-group-item-action.active{color:#fff;background-color:#141619;border-color:#141619}.btn-close{box-sizing:content-box;width:1em;height:1em;padding:.25em .25em;color:#000;background:transparent url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16' fill='%23000'%3e%3cpath d='M.293.293a1 1 0 011.414 0L8 6.586 14.293.293a1 1 0 111.414 1.414L9.414 8l6.293 6.293a1 1 0 01-1.414 1.414L8 9.414l-6.293 6.293a1 1 0 01-1.414-1.414L6.586 8 .293 1.707a1 1 0 010-1.414z'/%3e%3c/svg%3e") center/1em auto no-repeat;border:0;border-radius:.25rem;opacity:.5}.btn-close:hover{color:#000;text-decoration:none;opacity:.75}.btn-close:focus{outline:0;box-shadow:0 0 0 .25rem rgba(13,110,253,.25);opacity:1}.btn-close.disabled,.btn-close:disabled{pointer-events:none;-webkit-user-select:none;-moz-user-select:none;user-select:none;opacity:.25}.btn-close-white{filter:invert(1) grayscale(100%) brightness(200%)}.toast{width:350px;max-width:100%;font-size:.875rem;pointer-events:auto;background-color:rgba(255,255,255,.85);background-clip:padding-box;border:1px solid rgba(0,0,0,.1);box-shadow:0 .5rem 1rem rgba(0,0,0,.15);border-radius:.25rem}.toast.showing{opacity:0}.toast:not(.show){display:none}.toast-container{width:-webkit-max-content;width:-moz-max-content;width:max-content;max-width:100%;pointer-events:none}.toast-container>:not(:last-child){margin-bottom:.75rem}.toast-header{display:flex;align-items:center;padding:.5rem .75rem;color:#6c757d;background-color:rgba(255,255,255,.85);background-clip:padding-box;border-bottom:1px solid rgba(0,0,0,.05);border-top-left-radius:calc(.25rem - 1px);border-top-right-radius:calc(.25rem - 1px)}.toast-header .btn-close{margin-right:-.375rem;margin-left:.75rem}.toast-body{padding:.75rem;word-wrap:break-word}.modal{position:fixed;top:0;left:0;z-index:1055;display:none;width:100%;height:100%;overflow-x:hidden;overflow-y:auto;outline:0}.modal-dialog{position:relative;width:auto;margin:.5rem;pointer-events:none}.modal.fade .modal-dialog{transition:transform .3s ease-out;transform:translate(0,-50px)}@media (prefers-reduced-motion:reduce){.modal.fade .modal-dialog{transition:none}}.modal.show .modal-dialog{transform:none}.modal.modal-static .modal-dialog{transform:scale(1.02)}.modal-dialog-scrollable{height:calc(100% - 1rem)}.modal-dialog-scrollable .modal-content{max-height:100%;overflow:hidden}.modal-dialog-scrollable .modal-body{overflow-y:auto}.modal-dialog-centered{display:flex;align-items:center;min-height:calc(100% - 1rem)}.modal-content{position:relative;display:flex;flex-direction:column;width:100%;pointer-events:auto;background-color:#fff;background-clip:padding-box;border:1px solid rgba(0,0,0,.2);border-radius:.3rem;outline:0}.modal-backdrop{position:fixed;top:0;left:0;z-index:1050;width:100vw;height:100vh;background-color:#000}.modal-backdrop.fade{opacity:0}.modal-backdrop.show{opacity:.5}.modal-header{display:flex;flex-shrink:0;align-items:center;justify-content:space-between;padding:1rem 1rem;border-bottom:1px solid #dee2e6;border-top-left-radius:calc(.3rem - 1px);border-top-right-radius:calc(.3rem - 1px)}.modal-header .btn-close{padding:.5rem .5rem;margin:-.5rem -.5rem -.5rem auto}.modal-title{margin-bottom:0;line-height:1.5}.modal-body{position:relative;flex:1 1 auto;padding:1rem}.modal-footer{display:flex;flex-wrap:wrap;flex-shrink:0;align-items:center;justify-content:flex-end;padding:.75rem;border-top:1px solid #dee2e6;border-bottom-right-radius:calc(.3rem - 1px);border-bottom-left-radius:calc(.3rem - 1px)}.modal-footer>*{margin:.25rem}@media (min-width:576px){.modal-dialog{max-width:500px;margin:1.75rem auto}.modal-dialog-scrollable{height:calc(100% - 3.5rem)}.modal-dialog-centered{min-height:calc(100% - 3.5rem)}.modal-sm{max-width:300px}}@media (min-width:992px){.modal-lg,.modal-xl{max-width:800px}}@media (min-width:1200px){.modal-xl{max-width:1140px}}.modal-fullscreen{width:100vw;max-width:none;height:100%;margin:0}.modal-fullscreen .modal-content{height:100%;border:0;border-radius:0}.modal-fullscreen .modal-header{border-radius:0}.modal-fullscreen .modal-body{overflow-y:auto}.modal-fullscreen .modal-footer{border-radius:0}@media (max-width:575.98px){.modal-fullscreen-sm-down{width:100vw;max-width:none;height:100%;margin:0}.modal-fullscreen-sm-down .modal-content{height:100%;border:0;border-radius:0}.modal-fullscreen-sm-down .modal-header{border-radius:0}.modal-fullscreen-sm-down .modal-body{overflow-y:auto}.modal-fullscreen-sm-down .modal-footer{border-radius:0}}@media (max-width:767.98px){.modal-fullscreen-md-down{width:100vw;max-width:none;height:100%;margin:0}.modal-fullscreen-md-down .modal-content{height:100%;border:0;border-radius:0}.modal-fullscreen-md-down .modal-header{border-radius:0}.modal-fullscreen-md-down .modal-body{overflow-y:auto}.modal-fullscreen-md-down .modal-footer{border-radius:0}}@media (max-width:991.98px){.modal-fullscreen-lg-down{width:100vw;max-width:none;height:100%;margin:0}.modal-fullscreen-lg-down .modal-content{height:100%;border:0;border-radius:0}.modal-fullscreen-lg-down .modal-header{border-radius:0}.modal-fullscreen-lg-down .modal-body{overflow-y:auto}.modal-fullscreen-lg-down .modal-footer{border-radius:0}}@media (max-width:1199.98px){.modal-fullscreen-xl-down{width:100vw;max-width:none;height:100%;margin:0}.modal-fullscreen-xl-down .modal-content{height:100%;border:0;border-radius:0}.modal-fullscreen-xl-down .modal-header{border-radius:0}.modal-fullscreen-xl-down .modal-body{overflow-y:auto}.modal-fullscreen-xl-down .modal-footer{border-radius:0}}@media (max-width:1399.98px){.modal-fullscreen-xxl-down{width:100vw;max-width:none;height:100%;margin:0}.modal-fullscreen-xxl-down .modal-content{height:100%;border:0;border-radius:0}.modal-fullscreen-xxl-down .modal-header{border-radius:0}.modal-fullscreen-xxl-down .modal-body{overflow-y:auto}.modal-fullscreen-xxl-down .modal-footer{border-radius:0}}.tooltip{position:absolute;z-index:1080;display:block;margin:0;font-family:var(--bs-font-sans-serif);font-style:normal;font-weight:400;line-height:1.5;text-align:left;text-align:start;text-decoration:none;text-shadow:none;text-transform:none;letter-spacing:normal;word-break:normal;word-spacing:normal;white-space:normal;line-break:auto;font-size:.875rem;word-wrap:break-word;opacity:0}.tooltip.show{opacity:.9}.tooltip .tooltip-arrow{position:absolute;display:block;width:.8rem;height:.4rem}.tooltip .tooltip-arrow::before{position:absolute;content:"";border-color:transparent;border-style:solid}.bs-tooltip-auto[data-popper-placement^=top],.bs-tooltip-top{padding:.4rem 0}.bs-tooltip-auto[data-popper-placement^=top] .tooltip-arrow,.bs-tooltip-top .tooltip-arrow{bottom:0}.bs-tooltip-auto[data-popper-placement^=top] .tooltip-arrow::before,.bs-tooltip-top .tooltip-arrow::before{top:-1px;border-width:.4rem .4rem 0;border-top-color:#000}.bs-tooltip-auto[data-popper-placement^=right],.bs-tooltip-end{padding:0 .4rem}.bs-tooltip-auto[data-popper-placement^=right] .tooltip-arrow,.bs-tooltip-end .tooltip-arrow{left:0;width:.4rem;height:.8rem}.bs-tooltip-auto[data-popper-placement^=right] .tooltip-arrow::before,.bs-tooltip-end .tooltip-arrow::before{right:-1px;border-width:.4rem .4rem .4rem 0;border-right-color:#000}.bs-tooltip-auto[data-popper-placement^=bottom],.bs-tooltip-bottom{padding:.4rem 0}.bs-tooltip-auto[data-popper-placement^=bottom] .tooltip-arrow,.bs-tooltip-bottom .tooltip-arrow{top:0}.bs-tooltip-auto[data-popper-placement^=bottom] .tooltip-arrow::before,.bs-tooltip-bottom .tooltip-arrow::before{bottom:-1px;border-width:0 .4rem .4rem;border-bottom-color:#000}.bs-tooltip-auto[data-popper-placement^=left],.bs-tooltip-start{padding:0 .4rem}.bs-tooltip-auto[data-popper-placement^=left] .tooltip-arrow,.bs-tooltip-start .tooltip-arrow{right:0;width:.4rem;height:.8rem}.bs-tooltip-auto[data-popper-placement^=left] .tooltip-arrow::before,.bs-tooltip-start .tooltip-arrow::before{left:-1px;border-width:.4rem 0 .4rem .4rem;border-left-color:#000}.tooltip-inner{max-width:200px;padding:.25rem .5rem;color:#fff;text-align:center;background-color:#000;border-radius:.25rem}.popover{position:absolute;top:0;left:0;z-index:1070;display:block;max-width:276px;font-family:var(--bs-font-sans-serif);font-style:normal;font-weight:400;line-height:1.5;text-align:left;text-align:start;text-decoration:none;text-shadow:none;text-transform:none;letter-spacing:normal;word-break:normal;word-spacing:normal;white-space:normal;line-break:auto;font-size:.875rem;word-wrap:break-word;background-color:#fff;background-clip:padding-box;border:1px solid rgba(0,0,0,.2);border-radius:.3rem}.popover .popover-arrow{position:absolute;display:block;width:1rem;height:.5rem}.popover .popover-arrow::after,.popover .popover-arrow::before{position:absolute;display:block;content:"";border-color:transparent;border-style:solid}.bs-popover-auto[data-popper-placement^=top]>.popover-arrow,.bs-popover-top>.popover-arrow{bottom:calc(-.5rem - 1px)}.bs-popover-auto[data-popper-placement^=top]>.popover-arrow::before,.bs-popover-top>.popover-arrow::before{bottom:0;border-width:.5rem .5rem 0;border-top-color:rgba(0,0,0,.25)}.bs-popover-auto[data-popper-placement^=top]>.popover-arrow::after,.bs-popover-top>.popover-arrow::after{bottom:1px;border-width:.5rem .5rem 0;border-top-color:#fff}.bs-popover-auto[data-popper-placement^=right]>.popover-arrow,.bs-popover-end>.popover-arrow{left:calc(-.5rem - 1px);width:.5rem;height:1rem}.bs-popover-auto[data-popper-placement^=right]>.popover-arrow::before,.bs-popover-end>.popover-arrow::before{left:0;border-width:.5rem .5rem .5rem 0;border-right-color:rgba(0,0,0,.25)}.bs-popover-auto[data-popper-placement^=right]>.popover-arrow::after,.bs-popover-end>.popover-arrow::after{left:1px;border-width:.5rem .5rem .5rem 0;border-right-color:#fff}.bs-popover-auto[data-popper-placement^=bottom]>.popover-arrow,.bs-popover-bottom>.popover-arrow{top:calc(-.5rem - 1px)}.bs-popover-auto[data-popper-placement^=bottom]>.popover-arrow::before,.bs-popover-bottom>.popover-arrow::before{top:0;border-width:0 .5rem .5rem .5rem;border-bottom-color:rgba(0,0,0,.25)}.bs-popover-auto[data-popper-placement^=bottom]>.popover-arrow::after,.bs-popover-bottom>.popover-arrow::after{top:1px;border-width:0 .5rem .5rem .5rem;border-bottom-color:#fff}.bs-popover-auto[data-popper-placement^=bottom] .popover-header::before,.bs-popover-bottom .popover-header::before{position:absolute;top:0;left:50%;display:block;width:1rem;margin-left:-.5rem;content:"";border-bottom:1px solid #f0f0f0}.bs-popover-auto[data-popper-placement^=left]>.popover-arrow,.bs-popover-start>.popover-arrow{right:calc(-.5rem - 1px);width:.5rem;height:1rem}.bs-popover-auto[data-popper-placement^=left]>.popover-arrow::before,.bs-popover-start>.popover-arrow::before{right:0;border-width:.5rem 0 .5rem .5rem;border-left-color:rgba(0,0,0,.25)}.bs-popover-auto[data-popper-placement^=left]>.popover-arrow::after,.bs-popover-start>.popover-arrow::after{right:1px;border-width:.5rem 0 .5rem .5rem;border-left-color:#fff}.popover-header{padding:.5rem 1rem;margin-bottom:0;font-size:1rem;background-color:#f0f0f0;border-bottom:1px solid rgba(0,0,0,.2);border-top-left-radius:calc(.3rem - 1px);border-top-right-radius:calc(.3rem - 1px)}.popover-header:empty{display:none}.popover-body{padding:1rem 1rem;color:#212529}.carousel{position:relative}.carousel.pointer-event{touch-action:pan-y}.carousel-inner{position:relative;width:100%;overflow:hidden}.carousel-inner::after{display:block;clear:both;content:""}.carousel-item{position:relative;display:none;float:left;width:100%;margin-right:-100%;-webkit-backface-visibility:hidden;backface-visibility:hidden;transition:transform .6s ease-in-out}@media (prefers-reduced-motion:reduce){.carousel-item{transition:none}}.carousel-item-next,.carousel-item-prev,.carousel-item.active{display:block}.active.carousel-item-end,.carousel-item-next:not(.carousel-item-start){transform:translateX(100%)}.active.carousel-item-start,.carousel-item-prev:not(.carousel-item-end){transform:translateX(-100%)}.carousel-fade .carousel-item{opacity:0;transition-property:opacity;transform:none}.carousel-fade .carousel-item-next.carousel-item-start,.carousel-fade .carousel-item-prev.carousel-item-end,.carousel-fade .carousel-item.active{z-index:1;opacity:1}.carousel-fade .active.carousel-item-end,.carousel-fade .active.carousel-item-start{z-index:0;opacity:0;transition:opacity 0s .6s}@media (prefers-reduced-motion:reduce){.carousel-fade .active.carousel-item-end,.carousel-fade .active.carousel-item-start{transition:none}}.carousel-control-next,.carousel-control-prev{position:absolute;top:0;bottom:0;z-index:1;display:flex;align-items:center;justify-content:center;width:15%;padding:0;color:#fff;text-align:center;background:0 0;border:0;opacity:.5;transition:opacity .15s ease}@media (prefers-reduced-motion:reduce){.carousel-control-next,.carousel-control-prev{transition:none}}.carousel-control-next:focus,.carousel-control-next:hover,.carousel-control-prev:focus,.carousel-control-prev:hover{color:#fff;text-decoration:none;outline:0;opacity:.9}.carousel-control-prev{left:0}.carousel-control-next{right:0}.carousel-control-next-icon,.carousel-control-prev-icon{display:inline-block;width:2rem;height:2rem;background-repeat:no-repeat;background-position:50%;background-size:100% 100%}.carousel-control-prev-icon{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16' fill='%23fff'%3e%3cpath d='M11.354 1.646a.5.5 0 0 1 0 .708L5.707 8l5.647 5.646a.5.5 0 0 1-.708.708l-6-6a.5.5 0 0 1 0-.708l6-6a.5.5 0 0 1 .708 0z'/%3e%3c/svg%3e")}.carousel-control-next-icon{background-image:url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16' fill='%23fff'%3e%3cpath d='M4.646 1.646a.5.5 0 0 1 .708 0l6 6a.5.5 0 0 1 0 .708l-6 6a.5.5 0 0 1-.708-.708L10.293 8 4.646 2.354a.5.5 0 0 1 0-.708z'/%3e%3c/svg%3e")}.carousel-indicators{position:absolute;right:0;bottom:0;left:0;z-index:2;display:flex;justify-content:center;padding:0;margin-right:15%;margin-bottom:1rem;margin-left:15%;list-style:none}.carousel-indicators [data-bs-target]{box-sizing:content-box;flex:0 1 auto;width:30px;height:3px;padding:0;margin-right:3px;margin-left:3px;text-indent:-999px;cursor:pointer;background-color:#fff;background-clip:padding-box;border:0;border-top:10px solid transparent;border-bottom:10px solid transparent;opacity:.5;transition:opacity .6s ease}@media (prefers-reduced-motion:reduce){.carousel-indicators [data-bs-target]{transition:none}}.carousel-indicators .active{opacity:1}.carousel-caption{position:absolute;right:15%;bottom:1.25rem;left:15%;padding-top:1.25rem;padding-bottom:1.25rem;color:#fff;text-align:center}.carousel-dark .carousel-control-next-icon,.carousel-dark .carousel-control-prev-icon{filter:invert(1) grayscale(100)}.carousel-dark .carousel-indicators [data-bs-target]{background-color:#000}.carousel-dark .carousel-caption{color:#000}@-webkit-keyframes spinner-border{to{transform:rotate(360deg)}}@keyframes spinner-border{to{transform:rotate(360deg)}}.spinner-border{display:inline-block;width:2rem;height:2rem;vertical-align:-.125em;border:.25em solid currentColor;border-right-color:transparent;border-radius:50%;-webkit-animation:.75s linear infinite spinner-border;animation:.75s linear infinite spinner-border}.spinner-border-sm{width:1rem;height:1rem;border-width:.2em}@-webkit-keyframes spinner-grow{0%{transform:scale(0)}50%{opacity:1;transform:none}}@keyframes spinner-grow{0%{transform:scale(0)}50%{opacity:1;transform:none}}.spinner-grow{display:inline-block;width:2rem;height:2rem;vertical-align:-.125em;background-color:currentColor;border-radius:50%;opacity:0;-webkit-animation:.75s linear infinite spinner-grow;animation:.75s linear infinite spinner-grow}.spinner-grow-sm{width:1rem;height:1rem}@media (prefers-reduced-motion:reduce){.spinner-border,.spinner-grow{-webkit-animation-duration:1.5s;animation-duration:1.5s}}.offcanvas{position:fixed;bottom:0;z-index:1045;display:flex;flex-direction:column;max-width:100%;visibility:hidden;background-color:#fff;background-clip:padding-box;outline:0;transition:transform .3s ease-in-out}@media (prefers-reduced-motion:reduce){.offcanvas{transition:none}}.offcanvas-backdrop{position:fixed;top:0;left:0;z-index:1040;width:100vw;height:100vh;background-color:#000}.offcanvas-backdrop.fade{opacity:0}.offcanvas-backdrop.show{opacity:.5}.offcanvas-header{display:flex;align-items:center;justify-content:space-between;padding:1rem 1rem}.offcanvas-header .btn-close{padding:.5rem .5rem;margin-top:-.5rem;margin-right:-.5rem;margin-bottom:-.5rem}.offcanvas-title{margin-bottom:0;line-height:1.5}.offcanvas-body{flex-grow:1;padding:1rem 1rem;overflow-y:auto}.offcanvas-start{top:0;left:0;width:400px;border-right:1px solid rgba(0,0,0,.2);transform:translateX(-100%)}.offcanvas-end{top:0;right:0;width:400px;border-left:1px solid rgba(0,0,0,.2);transform:translateX(100%)}.offcanvas-top{top:0;right:0;left:0;height:30vh;max-height:100%;border-bottom:1px solid rgba(0,0,0,.2);transform:translateY(-100%)}.offcanvas-bottom{right:0;left:0;height:30vh;max-height:100%;border-top:1px solid rgba(0,0,0,.2);transform:translateY(100%)}.offcanvas.show{transform:none}.placeholder{display:inline-block;min-height:1em;vertical-align:middle;cursor:wait;background-color:currentColor;opacity:.5}.placeholder.btn::before{display:inline-block;content:""}.placeholder-xs{min-height:.6em}.placeholder-sm{min-height:.8em}.placeholder-lg{min-height:1.2em}.placeholder-glow .placeholder{-webkit-animation:placeholder-glow 2s ease-in-out infinite;animation:placeholder-glow 2s ease-in-out infinite}@-webkit-keyframes placeholder-glow{50%{opacity:.2}}@keyframes placeholder-glow{50%{opacity:.2}}.placeholder-wave{-webkit-mask-image:linear-gradient(130deg,#000 55%,rgba(0,0,0,0.8) 75%,#000 95%);mask-image:linear-gradient(130deg,#000 55%,rgba(0,0,0,0.8) 75%,#000 95%);-webkit-mask-size:200% 100%;mask-size:200% 100%;-webkit-animation:placeholder-wave 2s linear infinite;animation:placeholder-wave 2s linear infinite}@-webkit-keyframes placeholder-wave{100%{-webkit-mask-position:-200% 0%;mask-position:-200% 0%}}@keyframes placeholder-wave{100%{-webkit-mask-position:-200% 0%;mask-position:-200% 0%}}.clearfix::after{display:block;clear:both;content:""}.link-primary{color:#0d6efd}.link-primary:focus,.link-primary:hover{color:#0a58ca}.link-secondary{color:#6c757d}.link-secondary:focus,.link-secondary:hover{color:#565e64}.link-success{color:#198754}.link-success:focus,.link-success:hover{color:#146c43}.link-info{color:#0dcaf0}.link-info:focus,.link-info:hover{color:#3dd5f3}.link-warning{color:#ffc107}.link-warning:focus,.link-warning:hover{color:#ffcd39}.link-danger{color:#dc3545}.link-danger:focus,.link-danger:hover{color:#b02a37}.link-light{color:#f8f9fa}.link-light:focus,.link-light:hover{color:#f9fafb}.link-dark{color:#212529}.link-dark:focus,.link-dark:hover{color:#1a1e21}.ratio{position:relative;width:100%}.ratio::before{display:block;padding-top:var(--bs-aspect-ratio);content:""}.ratio>*{position:absolute;top:0;left:0;width:100%;height:100%}.ratio-1x1{--bs-aspect-ratio:100%}.ratio-4x3{--bs-aspect-ratio:calc(3 / 4 * 100%)}.ratio-16x9{--bs-aspect-ratio:calc(9 / 16 * 100%)}.ratio-21x9{--bs-aspect-ratio:calc(9 / 21 * 100%)}.fixed-top{position:fixed;top:0;right:0;left:0;z-index:1030}.fixed-bottom{position:fixed;right:0;bottom:0;left:0;z-index:1030}.sticky-top{position:-webkit-sticky;position:sticky;top:0;z-index:1020}@media (min-width:576px){.sticky-sm-top{position:-webkit-sticky;position:sticky;top:0;z-index:1020}}@media (min-width:768px){.sticky-md-top{position:-webkit-sticky;position:sticky;top:0;z-index:1020}}@media (min-width:992px){.sticky-lg-top{position:-webkit-sticky;position:sticky;top:0;z-index:1020}}@media (min-width:1200px){.sticky-xl-top{position:-webkit-sticky;position:sticky;top:0;z-index:1020}}@media (min-width:1400px){.sticky-xxl-top{position:-webkit-sticky;position:sticky;top:0;z-index:1020}}.hstack{display:flex;flex-direction:row;align-items:center;align-self:stretch}.vstack{display:flex;flex:1 1 auto;flex-direction:column;align-self:stretch}.visually-hidden,.visually-hidden-focusable:not(:focus):not(:focus-within){position:absolute!important;width:1px!important;height:1px!important;padding:0!important;margin:-1px!important;overflow:hidden!important;clip:rect(0,0,0,0)!important;white-space:nowrap!important;border:0!important}.stretched-link::after{position:absolute;top:0;right:0;bottom:0;left:0;z-index:1;content:""}.text-truncate{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.vr{display:inline-block;align-self:stretch;width:1px;min-height:1em;background-color:currentColor;opacity:.25}.align-baseline{vertical-align:baseline!important}.align-top{vertical-align:top!important}.align-middle{vertical-align:middle!important}.align-bottom{vertical-align:bottom!important}.align-text-bottom{vertical-align:text-bottom!important}.align-text-top{vertical-align:text-top!important}.float-start{float:left!important}.float-end{float:right!important}.float-none{float:none!important}.opacity-0{opacity:0!important}.opacity-25{opacity:.25!important}.opacity-50{opacity:.5!important}.opacity-75{opacity:.75!important}.opacity-100{opacity:1!important}.overflow-auto{overflow:auto!important}.overflow-hidden{overflow:hidden!important}.overflow-visible{overflow:visible!important}.overflow-scroll{overflow:scroll!important}.d-inline{display:inline!important}.d-inline-block{display:inline-block!important}.d-block{display:block!important}.d-grid{display:grid!important}.d-table{display:table!important}.d-table-row{display:table-row!important}.d-table-cell{display:table-cell!important}.d-flex{display:flex!important}.d-inline-flex{display:inline-flex!important}.d-none{display:none!important}.shadow{box-shadow:0 .5rem 1rem rgba(0,0,0,.15)!important}.shadow-sm{box-shadow:0 .125rem .25rem rgba(0,0,0,.075)!important}.shadow-lg{box-shadow:0 1rem 3rem rgba(0,0,0,.175)!important}.shadow-none{box-shadow:none!important}.position-static{position:static!important}.position-relative{position:relative!important}.position-absolute{position:absolute!important}.position-fixed{position:fixed!important}.position-sticky{position:-webkit-sticky!important;position:sticky!important}.top-0{top:0!important}.top-50{top:50%!important}.top-100{top:100%!important}.bottom-0{bottom:0!important}.bottom-50{bottom:50%!important}.bottom-100{bottom:100%!important}.start-0{left:0!important}.start-50{left:50%!important}.start-100{left:100%!important}.end-0{right:0!important}.end-50{right:50%!important}.end-100{right:100%!important}.translate-middle{transform:translate(-50%,-50%)!important}.translate-middle-x{transform:translateX(-50%)!important}.translate-middle-y{transform:translateY(-50%)!important}.border{border:1px solid #dee2e6!important}.border-0{border:0!important}.border-top{border-top:1px solid #dee2e6!important}.border-top-0{border-top:0!important}.border-end{border-right:1px solid #dee2e6!important}.border-end-0{border-right:0!important}.border-bottom{border-bottom:1px solid #dee2e6!important}.border-bottom-0{border-bottom:0!important}.border-start{border-left:1px solid #dee2e6!important}.border-start-0{border-left:0!important}.border-primary{border-color:#0d6efd!important}.border-secondary{border-color:#6c757d!important}.border-success{border-color:#198754!important}.border-info{border-color:#0dcaf0!important}.border-warning{border-color:#ffc107!important}.border-danger{border-color:#dc3545!important}.border-light{border-color:#f8f9fa!important}.border-dark{border-color:#212529!important}.border-white{border-color:#fff!important}.border-1{border-width:1px!important}.border-2{border-width:2px!important}.border-3{border-width:3px!important}.border-4{border-width:4px!important}.border-5{border-width:5px!important}.w-25{width:25%!important}.w-50{width:50%!important}.w-75{width:75%!important}.w-100{width:100%!important}.w-auto{width:auto!important}.mw-100{max-width:100%!important}.vw-100{width:100vw!important}.min-vw-100{min-width:100vw!important}.h-25{height:25%!important}.h-50{height:50%!important}.h-75{height:75%!important}.h-100{height:100%!important}.h-auto{height:auto!important}.mh-100{max-height:100%!important}.vh-100{height:100vh!important}.min-vh-100{min-height:100vh!important}.flex-fill{flex:1 1 auto!important}.flex-row{flex-direction:row!important}.flex-column{flex-direction:column!important}.flex-row-reverse{flex-direction:row-reverse!important}.flex-column-reverse{flex-direction:column-reverse!important}.flex-grow-0{flex-grow:0!important}.flex-grow-1{flex-grow:1!important}.flex-shrink-0{flex-shrink:0!important}.flex-shrink-1{flex-shrink:1!important}.flex-wrap{flex-wrap:wrap!important}.flex-nowrap{flex-wrap:nowrap!important}.flex-wrap-reverse{flex-wrap:wrap-reverse!important}.gap-0{gap:0!important}.gap-1{gap:.25rem!important}.gap-2{gap:.5rem!important}.gap-3{gap:1rem!important}.gap-4{gap:1.5rem!important}.gap-5{gap:3rem!important}.justify-content-start{justify-content:flex-start!important}.justify-content-end{justify-content:flex-end!important}.justify-content-center{justify-content:center!important}.justify-content-between{justify-content:space-between!important}.justify-content-around{justify-content:space-around!important}.justify-content-evenly{justify-content:space-evenly!important}.align-items-start{align-items:flex-start!important}.align-items-end{align-items:flex-end!important}.align-items-center{align-items:center!important}.align-items-baseline{align-items:baseline!important}.align-items-stretch{align-items:stretch!important}.align-content-start{align-content:flex-start!important}.align-content-end{align-content:flex-end!important}.align-content-center{align-content:center!important}.align-content-between{align-content:space-between!important}.align-content-around{align-content:space-around!important}.align-content-stretch{align-content:stretch!important}.align-self-auto{align-self:auto!important}.align-self-start{align-self:flex-start!important}.align-self-end{align-self:flex-end!important}.align-self-center{align-self:center!important}.align-self-baseline{align-self:baseline!important}.align-self-stretch{align-self:stretch!important}.order-first{order:-1!important}.order-0{order:0!important}.order-1{order:1!important}.order-2{order:2!important}.order-3{order:3!important}.order-4{order:4!important}.order-5{order:5!important}.order-last{order:6!important}.m-0{margin:0!important}.m-1{margin:.25rem!important}.m-2{margin:.5rem!important}.m-3{margin:1rem!important}.m-4{margin:1.5rem!important}.m-5{margin:3rem!important}.m-auto{margin:auto!important}.mx-0{margin-right:0!important;margin-left:0!important}.mx-1{margin-right:.25rem!important;margin-left:.25rem!important}.mx-2{margin-right:.5rem!important;margin-left:.5rem!important}.mx-3{margin-right:1rem!important;margin-left:1rem!important}.mx-4{margin-right:1.5rem!important;margin-left:1.5rem!important}.mx-5{margin-right:3rem!important;margin-left:3rem!important}.mx-auto{margin-right:auto!important;margin-left:auto!important}.my-0{margin-top:0!important;margin-bottom:0!important}.my-1{margin-top:.25rem!important;margin-bottom:.25rem!important}.my-2{margin-top:.5rem!important;margin-bottom:.5rem!important}.my-3{margin-top:1rem!important;margin-bottom:1rem!important}.my-4{margin-top:1.5rem!important;margin-bottom:1.5rem!important}.my-5{margin-top:3rem!important;margin-bottom:3rem!important}.my-auto{margin-top:auto!important;margin-bottom:auto!important}.mt-0{margin-top:0!important}.mt-1{margin-top:.25rem!important}.mt-2{margin-top:.5rem!important}.mt-3{margin-top:1rem!important}.mt-4{margin-top:1.5rem!important}.mt-5{margin-top:3rem!important}.mt-auto{margin-top:auto!important}.me-0{margin-right:0!important}.me-1{margin-right:.25rem!important}.me-2{margin-right:.5rem!important}.me-3{margin-right:1rem!important}.me-4{margin-right:1.5rem!important}.me-5{margin-right:3rem!important}.me-auto{margin-right:auto!important}.mb-0{margin-bottom:0!important}.mb-1{margin-bottom:.25rem!important}.mb-2{margin-bottom:.5rem!important}.mb-3{margin-bottom:1rem!important}.mb-4{margin-bottom:1.5rem!important}.mb-5{margin-bottom:3rem!important}.mb-auto{margin-bottom:auto!important}.ms-0{margin-left:0!important}.ms-1{margin-left:.25rem!important}.ms-2{margin-left:.5rem!important}.ms-3{margin-left:1rem!important}.ms-4{margin-left:1.5rem!important}.ms-5{margin-left:3rem!important}.ms-auto{margin-left:auto!important}.p-0{padding:0!important}.p-1{padding:.25rem!important}.p-2{padding:.5rem!important}.p-3{padding:1rem!important}.p-4{padding:1.5rem!important}.p-5{padding:3rem!important}.px-0{padding-right:0!important;padding-left:0!important}.px-1{padding-right:.25rem!important;padding-left:.25rem!important}.px-2{padding-right:.5rem!important;padding-left:.5rem!important}.px-3{padding-right:1rem!important;padding-left:1rem!important}.px-4{padding-right:1.5rem!important;padding-left:1.5rem!important}.px-5{padding-right:3rem!important;padding-left:3rem!important}.py-0{padding-top:0!important;padding-bottom:0!important}.py-1{padding-top:.25rem!important;padding-bottom:.25rem!important}.py-2{padding-top:.5rem!important;padding-bottom:.5rem!important}.py-3{padding-top:1rem!important;padding-bottom:1rem!important}.py-4{padding-top:1.5rem!important;padding-bottom:1.5rem!important}.py-5{padding-top:3rem!important;padding-bottom:3rem!important}.pt-0{padding-top:0!important}.pt-1{padding-top:.25rem!important}.pt-2{padding-top:.5rem!important}.pt-3{padding-top:1rem!important}.pt-4{padding-top:1.5rem!important}.pt-5{padding-top:3rem!important}.pe-0{padding-right:0!important}.pe-1{padding-right:.25rem!important}.pe-2{padding-right:.5rem!important}.pe-3{padding-right:1rem!important}.pe-4{padding-right:1.5rem!important}.pe-5{padding-right:3rem!important}.pb-0{padding-bottom:0!important}.pb-1{padding-bottom:.25rem!important}.pb-2{padding-bottom:.5rem!important}.pb-3{padding-bottom:1rem!important}.pb-4{padding-bottom:1.5rem!important}.pb-5{padding-bottom:3rem!important}.ps-0{padding-left:0!important}.ps-1{padding-left:.25rem!important}.ps-2{padding-left:.5rem!important}.ps-3{padding-left:1rem!important}.ps-4{padding-left:1.5rem!important}.ps-5{padding-left:3rem!important}.font-monospace{font-family:var(--bs-font-monospace)!important}.fs-1{font-size:calc(1.375rem + 1.5vw)!important}.fs-2{font-size:calc(1.325rem + .9vw)!important}.fs-3{font-size:calc(1.3rem + .6vw)!important}.fs-4{font-size:calc(1.275rem + .3vw)!important}.fs-5{font-size:1.25rem!important}.fs-6{font-size:1rem!important}.fst-italic{font-style:italic!important}.fst-normal{font-style:normal!important}.fw-light{font-weight:300!important}.fw-lighter{font-weight:lighter!important}.fw-normal{font-weight:400!important}.fw-bold{font-weight:700!important}.fw-bolder{font-weight:bolder!important}.lh-1{line-height:1!important}.lh-sm{line-height:1.25!important}.lh-base{line-height:1.5!important}.lh-lg{line-height:2!important}.text-start{text-align:left!important}.text-end{text-align:right!important}.text-center{text-align:center!important}.text-decoration-none{text-decoration:none!important}.text-decoration-underline{text-decoration:underline!important}.text-decoration-line-through{text-decoration:line-through!important}.text-lowercase{text-transform:lowercase!important}.text-uppercase{text-transform:uppercase!important}.text-capitalize{text-transform:capitalize!important}.text-wrap{white-space:normal!important}.text-nowrap{white-space:nowrap!important}.text-break{word-wrap:break-word!important;word-break:break-word!important}.text-primary{--bs-text-opacity:1;color:rgba(var(--bs-primary-rgb),var(--bs-text-opacity))!important}.text-secondary{--bs-text-opacity:1;color:rgba(var(--bs-secondary-rgb),var(--bs-text-opacity))!important}.text-success{--bs-text-opacity:1;color:rgba(var(--bs-success-rgb),var(--bs-text-opacity))!important}.text-info{--bs-text-opacity:1;color:rgba(var(--bs-info-rgb),var(--bs-text-opacity))!important}.text-warning{--bs-text-opacity:1;color:rgba(var(--bs-warning-rgb),var(--bs-text-opacity))!important}.text-danger{--bs-text-opacity:1;color:rgba(var(--bs-danger-rgb),var(--bs-text-opacity))!important}.text-light{--bs-text-opacity:1;color:rgba(var(--bs-light-rgb),var(--bs-text-opacity))!important}.text-dark{--bs-text-opacity:1;color:rgba(var(--bs-dark-rgb),var(--bs-text-opacity))!important}.text-black{--bs-text-opacity:1;color:rgba(var(--bs-black-rgb),var(--bs-text-opacity))!important}.text-white{--bs-text-opacity:1;color:rgba(var(--bs-white-rgb),var(--bs-text-opacity))!important}.text-body{--bs-text-opacity:1;color:rgba(var(--bs-body-rgb),var(--bs-text-opacity))!important}.text-muted{--bs-text-opacity:1;color:#6c757d!important}.text-black-50{--bs-text-opacity:1;color:rgba(0,0,0,.5)!important}.text-white-50{--bs-text-opacity:1;color:rgba(255,255,255,.5)!important}.text-reset{--bs-text-opacity:1;color:inherit!important}.text-opacity-25{--bs-text-opacity:0.25}.text-opacity-50{--bs-text-opacity:0.5}.text-opacity-75{--bs-text-opacity:0.75}.text-opacity-100{--bs-text-opacity:1}.bg-primary{--bs-bg-opacity:1;background-color:rgba(var(--bs-primary-rgb),var(--bs-bg-opacity))!important}.bg-secondary{--bs-bg-opacity:1;background-color:rgba(var(--bs-secondary-rgb),var(--bs-bg-opacity))!important}.bg-success{--bs-bg-opacity:1;background-color:rgba(var(--bs-success-rgb),var(--bs-bg-opacity))!important}.bg-info{--bs-bg-opacity:1;background-color:rgba(var(--bs-info-rgb),var(--bs-bg-opacity))!important}.bg-warning{--bs-bg-opacity:1;background-color:rgba(var(--bs-warning-rgb),var(--bs-bg-opacity))!important}.bg-danger{--bs-bg-opacity:1;background-color:rgba(var(--bs-danger-rgb),var(--bs-bg-opacity))!important}.bg-light{--bs-bg-opacity:1;background-color:rgba(var(--bs-light-rgb),var(--bs-bg-opacity))!important}.bg-dark{--bs-bg-opacity:1;background-color:rgba(var(--bs-dark-rgb),var(--bs-bg-opacity))!important}.bg-black{--bs-bg-opacity:1;background-color:rgba(var(--bs-black-rgb),var(--bs-bg-opacity))!important}.bg-white{--bs-bg-opacity:1;background-color:rgba(var(--bs-white-rgb),var(--bs-bg-opacity))!important}.bg-body{--bs-bg-opacity:1;background-color:rgba(var(--bs-body-rgb),var(--bs-bg-opacity))!important}.bg-transparent{--bs-bg-opacity:1;background-color:transparent!important}.bg-opacity-10{--bs-bg-opacity:0.1}.bg-opacity-25{--bs-bg-opacity:0.25}.bg-opacity-50{--bs-bg-opacity:0.5}.bg-opacity-75{--bs-bg-opacity:0.75}.bg-opacity-100{--bs-bg-opacity:1}.bg-gradient{background-image:var(--bs-gradient)!important}.user-select-all{-webkit-user-select:all!important;-moz-user-select:all!important;user-select:all!important}.user-select-auto{-webkit-user-select:auto!important;-moz-user-select:auto!important;user-select:auto!important}.user-select-none{-webkit-user-select:none!important;-moz-user-select:none!important;user-select:none!important}.pe-none{pointer-events:none!important}.pe-auto{pointer-events:auto!important}.rounded{border-radius:.25rem!important}.rounded-0{border-radius:0!important}.rounded-1{border-radius:.2rem!important}.rounded-2{border-radius:.25rem!important}.rounded-3{border-radius:.3rem!important}.rounded-circle{border-radius:50%!important}.rounded-pill{border-radius:50rem!important}.rounded-top{border-top-left-radius:.25rem!important;border-top-right-radius:.25rem!important}.rounded-end{border-top-right-radius:.25rem!important;border-bottom-right-radius:.25rem!important}.rounded-bottom{border-bottom-right-radius:.25rem!important;border-bottom-left-radius:.25rem!important}.rounded-start{border-bottom-left-radius:.25rem!important;border-top-left-radius:.25rem!important}.visible{visibility:visible!important}.invisible{visibility:hidden!important}@media (min-width:576px){.float-sm-start{float:left!important}.float-sm-end{float:right!important}.float-sm-none{float:none!important}.d-sm-inline{display:inline!important}.d-sm-inline-block{display:inline-block!important}.d-sm-block{display:block!important}.d-sm-grid{display:grid!important}.d-sm-table{display:table!important}.d-sm-table-row{display:table-row!important}.d-sm-table-cell{display:table-cell!important}.d-sm-flex{display:flex!important}.d-sm-inline-flex{display:inline-flex!important}.d-sm-none{display:none!important}.flex-sm-fill{flex:1 1 auto!important}.flex-sm-row{flex-direction:row!important}.flex-sm-column{flex-direction:column!important}.flex-sm-row-reverse{flex-direction:row-reverse!important}.flex-sm-column-reverse{flex-direction:column-reverse!important}.flex-sm-grow-0{flex-grow:0!important}.flex-sm-grow-1{flex-grow:1!important}.flex-sm-shrink-0{flex-shrink:0!important}.flex-sm-shrink-1{flex-shrink:1!important}.flex-sm-wrap{flex-wrap:wrap!important}.flex-sm-nowrap{flex-wrap:nowrap!important}.flex-sm-wrap-reverse{flex-wrap:wrap-reverse!important}.gap-sm-0{gap:0!important}.gap-sm-1{gap:.25rem!important}.gap-sm-2{gap:.5rem!important}.gap-sm-3{gap:1rem!important}.gap-sm-4{gap:1.5rem!important}.gap-sm-5{gap:3rem!important}.justify-content-sm-start{justify-content:flex-start!important}.justify-content-sm-end{justify-content:flex-end!important}.justify-content-sm-center{justify-content:center!important}.justify-content-sm-between{justify-content:space-between!important}.justify-content-sm-around{justify-content:space-around!important}.justify-content-sm-evenly{justify-content:space-evenly!important}.align-items-sm-start{align-items:flex-start!important}.align-items-sm-end{align-items:flex-end!important}.align-items-sm-center{align-items:center!important}.align-items-sm-baseline{align-items:baseline!important}.align-items-sm-stretch{align-items:stretch!important}.align-content-sm-start{align-content:flex-start!important}.align-content-sm-end{align-content:flex-end!important}.align-content-sm-center{align-content:center!important}.align-content-sm-between{align-content:space-between!important}.align-content-sm-around{align-content:space-around!important}.align-content-sm-stretch{align-content:stretch!important}.align-self-sm-auto{align-self:auto!important}.align-self-sm-start{align-self:flex-start!important}.align-self-sm-end{align-self:flex-end!important}.align-self-sm-center{align-self:center!important}.align-self-sm-baseline{align-self:baseline!important}.align-self-sm-stretch{align-self:stretch!important}.order-sm-first{order:-1!important}.order-sm-0{order:0!important}.order-sm-1{order:1!important}.order-sm-2{order:2!important}.order-sm-3{order:3!important}.order-sm-4{order:4!important}.order-sm-5{order:5!important}.order-sm-last{order:6!important}.m-sm-0{margin:0!important}.m-sm-1{margin:.25rem!important}.m-sm-2{margin:.5rem!important}.m-sm-3{margin:1rem!important}.m-sm-4{margin:1.5rem!important}.m-sm-5{margin:3rem!important}.m-sm-auto{margin:auto!important}.mx-sm-0{margin-right:0!important;margin-left:0!important}.mx-sm-1{margin-right:.25rem!important;margin-left:.25rem!important}.mx-sm-2{margin-right:.5rem!important;margin-left:.5rem!important}.mx-sm-3{margin-right:1rem!important;margin-left:1rem!important}.mx-sm-4{margin-right:1.5rem!important;margin-left:1.5rem!important}.mx-sm-5{margin-right:3rem!important;margin-left:3rem!important}.mx-sm-auto{margin-right:auto!important;margin-left:auto!important}.my-sm-0{margin-top:0!important;margin-bottom:0!important}.my-sm-1{margin-top:.25rem!important;margin-bottom:.25rem!important}.my-sm-2{margin-top:.5rem!important;margin-bottom:.5rem!important}.my-sm-3{margin-top:1rem!important;margin-bottom:1rem!important}.my-sm-4{margin-top:1.5rem!important;margin-bottom:1.5rem!important}.my-sm-5{margin-top:3rem!important;margin-bottom:3rem!important}.my-sm-auto{margin-top:auto!important;margin-bottom:auto!important}.mt-sm-0{margin-top:0!important}.mt-sm-1{margin-top:.25rem!important}.mt-sm-2{margin-top:.5rem!important}.mt-sm-3{margin-top:1rem!important}.mt-sm-4{margin-top:1.5rem!important}.mt-sm-5{margin-top:3rem!important}.mt-sm-auto{margin-top:auto!important}.me-sm-0{margin-right:0!important}.me-sm-1{margin-right:.25rem!important}.me-sm-2{margin-right:.5rem!important}.me-sm-3{margin-right:1rem!important}.me-sm-4{margin-right:1.5rem!important}.me-sm-5{margin-right:3rem!important}.me-sm-auto{margin-right:auto!important}.mb-sm-0{margin-bottom:0!important}.mb-sm-1{margin-bottom:.25rem!important}.mb-sm-2{margin-bottom:.5rem!important}.mb-sm-3{margin-bottom:1rem!important}.mb-sm-4{margin-bottom:1.5rem!important}.mb-sm-5{margin-bottom:3rem!important}.mb-sm-auto{margin-bottom:auto!important}.ms-sm-0{margin-left:0!important}.ms-sm-1{margin-left:.25rem!important}.ms-sm-2{margin-left:.5rem!important}.ms-sm-3{margin-left:1rem!important}.ms-sm-4{margin-left:1.5rem!important}.ms-sm-5{margin-left:3rem!important}.ms-sm-auto{margin-left:auto!important}.p-sm-0{padding:0!important}.p-sm-1{padding:.25rem!important}.p-sm-2{padding:.5rem!important}.p-sm-3{padding:1rem!important}.p-sm-4{padding:1.5rem!important}.p-sm-5{padding:3rem!important}.px-sm-0{padding-right:0!important;padding-left:0!important}.px-sm-1{padding-right:.25rem!important;padding-left:.25rem!important}.px-sm-2{padding-right:.5rem!important;padding-left:.5rem!important}.px-sm-3{padding-right:1rem!important;padding-left:1rem!important}.px-sm-4{padding-right:1.5rem!important;padding-left:1.5rem!important}.px-sm-5{padding-right:3rem!important;padding-left:3rem!important}.py-sm-0{padding-top:0!important;padding-bottom:0!important}.py-sm-1{padding-top:.25rem!important;padding-bottom:.25rem!important}.py-sm-2{padding-top:.5rem!important;padding-bottom:.5rem!important}.py-sm-3{padding-top:1rem!important;padding-bottom:1rem!important}.py-sm-4{padding-top:1.5rem!important;padding-bottom:1.5rem!important}.py-sm-5{padding-top:3rem!important;padding-bottom:3rem!important}.pt-sm-0{padding-top:0!important}.pt-sm-1{padding-top:.25rem!important}.pt-sm-2{padding-top:.5rem!important}.pt-sm-3{padding-top:1rem!important}.pt-sm-4{padding-top:1.5rem!important}.pt-sm-5{padding-top:3rem!important}.pe-sm-0{padding-right:0!important}.pe-sm-1{padding-right:.25rem!important}.pe-sm-2{padding-right:.5rem!important}.pe-sm-3{padding-right:1rem!important}.pe-sm-4{padding-right:1.5rem!important}.pe-sm-5{padding-right:3rem!important}.pb-sm-0{padding-bottom:0!important}.pb-sm-1{padding-bottom:.25rem!important}.pb-sm-2{padding-bottom:.5rem!important}.pb-sm-3{padding-bottom:1rem!important}.pb-sm-4{padding-bottom:1.5rem!important}.pb-sm-5{padding-bottom:3rem!important}.ps-sm-0{padding-left:0!important}.ps-sm-1{padding-left:.25rem!important}.ps-sm-2{padding-left:.5rem!important}.ps-sm-3{padding-left:1rem!important}.ps-sm-4{padding-left:1.5rem!important}.ps-sm-5{padding-left:3rem!important}.text-sm-start{text-align:left!important}.text-sm-end{text-align:right!important}.text-sm-center{text-align:center!important}}@media (min-width:768px){.float-md-start{float:left!important}.float-md-end{float:right!important}.float-md-none{float:none!important}.d-md-inline{display:inline!important}.d-md-inline-block{display:inline-block!important}.d-md-block{display:block!important}.d-md-grid{display:grid!important}.d-md-table{display:table!important}.d-md-table-row{display:table-row!important}.d-md-table-cell{display:table-cell!important}.d-md-flex{display:flex!important}.d-md-inline-flex{display:inline-flex!important}.d-md-none{display:none!important}.flex-md-fill{flex:1 1 auto!important}.flex-md-row{flex-direction:row!important}.flex-md-column{flex-direction:column!important}.flex-md-row-reverse{flex-direction:row-reverse!important}.flex-md-column-reverse{flex-direction:column-reverse!important}.flex-md-grow-0{flex-grow:0!important}.flex-md-grow-1{flex-grow:1!important}.flex-md-shrink-0{flex-shrink:0!important}.flex-md-shrink-1{flex-shrink:1!important}.flex-md-wrap{flex-wrap:wrap!important}.flex-md-nowrap{flex-wrap:nowrap!important}.flex-md-wrap-reverse{flex-wrap:wrap-reverse!important}.gap-md-0{gap:0!important}.gap-md-1{gap:.25rem!important}.gap-md-2{gap:.5rem!important}.gap-md-3{gap:1rem!important}.gap-md-4{gap:1.5rem!important}.gap-md-5{gap:3rem!important}.justify-content-md-start{justify-content:flex-start!important}.justify-content-md-end{justify-content:flex-end!important}.justify-content-md-center{justify-content:center!important}.justify-content-md-between{justify-content:space-between!important}.justify-content-md-around{justify-content:space-around!important}.justify-content-md-evenly{justify-content:space-evenly!important}.align-items-md-start{align-items:flex-start!important}.align-items-md-end{align-items:flex-end!important}.align-items-md-center{align-items:center!important}.align-items-md-baseline{align-items:baseline!important}.align-items-md-stretch{align-items:stretch!important}.align-content-md-start{align-content:flex-start!important}.align-content-md-end{align-content:flex-end!important}.align-content-md-center{align-content:center!important}.align-content-md-between{align-content:space-between!important}.align-content-md-around{align-content:space-around!important}.align-content-md-stretch{align-content:stretch!important}.align-self-md-auto{align-self:auto!important}.align-self-md-start{align-self:flex-start!important}.align-self-md-end{align-self:flex-end!important}.align-self-md-center{align-self:center!important}.align-self-md-baseline{align-self:baseline!important}.align-self-md-stretch{align-self:stretch!important}.order-md-first{order:-1!important}.order-md-0{order:0!important}.order-md-1{order:1!important}.order-md-2{order:2!important}.order-md-3{order:3!important}.order-md-4{order:4!important}.order-md-5{order:5!important}.order-md-last{order:6!important}.m-md-0{margin:0!important}.m-md-1{margin:.25rem!important}.m-md-2{margin:.5rem!important}.m-md-3{margin:1rem!important}.m-md-4{margin:1.5rem!important}.m-md-5{margin:3rem!important}.m-md-auto{margin:auto!important}.mx-md-0{margin-right:0!important;margin-left:0!important}.mx-md-1{margin-right:.25rem!important;margin-left:.25rem!important}.mx-md-2{margin-right:.5rem!important;margin-left:.5rem!important}.mx-md-3{margin-right:1rem!important;margin-left:1rem!important}.mx-md-4{margin-right:1.5rem!important;margin-left:1.5rem!important}.mx-md-5{margin-right:3rem!important;margin-left:3rem!important}.mx-md-auto{margin-right:auto!important;margin-left:auto!important}.my-md-0{margin-top:0!important;margin-bottom:0!important}.my-md-1{margin-top:.25rem!important;margin-bottom:.25rem!important}.my-md-2{margin-top:.5rem!important;margin-bottom:.5rem!important}.my-md-3{margin-top:1rem!important;margin-bottom:1rem!important}.my-md-4{margin-top:1.5rem!important;margin-bottom:1.5rem!important}.my-md-5{margin-top:3rem!important;margin-bottom:3rem!important}.my-md-auto{margin-top:auto!important;margin-bottom:auto!important}.mt-md-0{margin-top:0!important}.mt-md-1{margin-top:.25rem!important}.mt-md-2{margin-top:.5rem!important}.mt-md-3{margin-top:1rem!important}.mt-md-4{margin-top:1.5rem!important}.mt-md-5{margin-top:3rem!important}.mt-md-auto{margin-top:auto!important}.me-md-0{margin-right:0!important}.me-md-1{margin-right:.25rem!important}.me-md-2{margin-right:.5rem!important}.me-md-3{margin-right:1rem!important}.me-md-4{margin-right:1.5rem!important}.me-md-5{margin-right:3rem!important}.me-md-auto{margin-right:auto!important}.mb-md-0{margin-bottom:0!important}.mb-md-1{margin-bottom:.25rem!important}.mb-md-2{margin-bottom:.5rem!important}.mb-md-3{margin-bottom:1rem!important}.mb-md-4{margin-bottom:1.5rem!important}.mb-md-5{margin-bottom:3rem!important}.mb-md-auto{margin-bottom:auto!important}.ms-md-0{margin-left:0!important}.ms-md-1{margin-left:.25rem!important}.ms-md-2{margin-left:.5rem!important}.ms-md-3{margin-left:1rem!important}.ms-md-4{margin-left:1.5rem!important}.ms-md-5{margin-left:3rem!important}.ms-md-auto{margin-left:auto!important}.p-md-0{padding:0!important}.p-md-1{padding:.25rem!important}.p-md-2{padding:.5rem!important}.p-md-3{padding:1rem!important}.p-md-4{padding:1.5rem!important}.p-md-5{padding:3rem!important}.px-md-0{padding-right:0!important;padding-left:0!important}.px-md-1{padding-right:.25rem!important;padding-left:.25rem!important}.px-md-2{padding-right:.5rem!important;padding-left:.5rem!important}.px-md-3{padding-right:1rem!important;padding-left:1rem!important}.px-md-4{padding-right:1.5rem!important;padding-left:1.5rem!important}.px-md-5{padding-right:3rem!important;padding-left:3rem!important}.py-md-0{padding-top:0!important;padding-bottom:0!important}.py-md-1{padding-top:.25rem!important;padding-bottom:.25rem!important}.py-md-2{padding-top:.5rem!important;padding-bottom:.5rem!important}.py-md-3{padding-top:1rem!important;padding-bottom:1rem!important}.py-md-4{padding-top:1.5rem!important;padding-bottom:1.5rem!important}.py-md-5{padding-top:3rem!important;padding-bottom:3rem!important}.pt-md-0{padding-top:0!important}.pt-md-1{padding-top:.25rem!important}.pt-md-2{padding-top:.5rem!important}.pt-md-3{padding-top:1rem!important}.pt-md-4{padding-top:1.5rem!important}.pt-md-5{padding-top:3rem!important}.pe-md-0{padding-right:0!important}.pe-md-1{padding-right:.25rem!important}.pe-md-2{padding-right:.5rem!important}.pe-md-3{padding-right:1rem!important}.pe-md-4{padding-right:1.5rem!important}.pe-md-5{padding-right:3rem!important}.pb-md-0{padding-bottom:0!important}.pb-md-1{padding-bottom:.25rem!important}.pb-md-2{padding-bottom:.5rem!important}.pb-md-3{padding-bottom:1rem!important}.pb-md-4{padding-bottom:1.5rem!important}.pb-md-5{padding-bottom:3rem!important}.ps-md-0{padding-left:0!important}.ps-md-1{padding-left:.25rem!important}.ps-md-2{padding-left:.5rem!important}.ps-md-3{padding-left:1rem!important}.ps-md-4{padding-left:1.5rem!important}.ps-md-5{padding-left:3rem!important}.text-md-start{text-align:left!important}.text-md-end{text-align:right!important}.text-md-center{text-align:center!important}}@media (min-width:992px){.float-lg-start{float:left!important}.float-lg-end{float:right!important}.float-lg-none{float:none!important}.d-lg-inline{display:inline!important}.d-lg-inline-block{display:inline-block!important}.d-lg-block{display:block!important}.d-lg-grid{display:grid!important}.d-lg-table{display:table!important}.d-lg-table-row{display:table-row!important}.d-lg-table-cell{display:table-cell!important}.d-lg-flex{display:flex!important}.d-lg-inline-flex{display:inline-flex!important}.d-lg-none{display:none!important}.flex-lg-fill{flex:1 1 auto!important}.flex-lg-row{flex-direction:row!important}.flex-lg-column{flex-direction:column!important}.flex-lg-row-reverse{flex-direction:row-reverse!important}.flex-lg-column-reverse{flex-direction:column-reverse!important}.flex-lg-grow-0{flex-grow:0!important}.flex-lg-grow-1{flex-grow:1!important}.flex-lg-shrink-0{flex-shrink:0!important}.flex-lg-shrink-1{flex-shrink:1!important}.flex-lg-wrap{flex-wrap:wrap!important}.flex-lg-nowrap{flex-wrap:nowrap!important}.flex-lg-wrap-reverse{flex-wrap:wrap-reverse!important}.gap-lg-0{gap:0!important}.gap-lg-1{gap:.25rem!important}.gap-lg-2{gap:.5rem!important}.gap-lg-3{gap:1rem!important}.gap-lg-4{gap:1.5rem!important}.gap-lg-5{gap:3rem!important}.justify-content-lg-start{justify-content:flex-start!important}.justify-content-lg-end{justify-content:flex-end!important}.justify-content-lg-center{justify-content:center!important}.justify-content-lg-between{justify-content:space-between!important}.justify-content-lg-around{justify-content:space-around!important}.justify-content-lg-evenly{justify-content:space-evenly!important}.align-items-lg-start{align-items:flex-start!important}.align-items-lg-end{align-items:flex-end!important}.align-items-lg-center{align-items:center!important}.align-items-lg-baseline{align-items:baseline!important}.align-items-lg-stretch{align-items:stretch!important}.align-content-lg-start{align-content:flex-start!important}.align-content-lg-end{align-content:flex-end!important}.align-content-lg-center{align-content:center!important}.align-content-lg-between{align-content:space-between!important}.align-content-lg-around{align-content:space-around!important}.align-content-lg-stretch{align-content:stretch!important}.align-self-lg-auto{align-self:auto!important}.align-self-lg-start{align-self:flex-start!important}.align-self-lg-end{align-self:flex-end!important}.align-self-lg-center{align-self:center!important}.align-self-lg-baseline{align-self:baseline!important}.align-self-lg-stretch{align-self:stretch!important}.order-lg-first{order:-1!important}.order-lg-0{order:0!important}.order-lg-1{order:1!important}.order-lg-2{order:2!important}.order-lg-3{order:3!important}.order-lg-4{order:4!important}.order-lg-5{order:5!important}.order-lg-last{order:6!important}.m-lg-0{margin:0!important}.m-lg-1{margin:.25rem!important}.m-lg-2{margin:.5rem!important}.m-lg-3{margin:1rem!important}.m-lg-4{margin:1.5rem!important}.m-lg-5{margin:3rem!important}.m-lg-auto{margin:auto!important}.mx-lg-0{margin-right:0!important;margin-left:0!important}.mx-lg-1{margin-right:.25rem!important;margin-left:.25rem!important}.mx-lg-2{margin-right:.5rem!important;margin-left:.5rem!important}.mx-lg-3{margin-right:1rem!important;margin-left:1rem!important}.mx-lg-4{margin-right:1.5rem!important;margin-left:1.5rem!important}.mx-lg-5{margin-right:3rem!important;margin-left:3rem!important}.mx-lg-auto{margin-right:auto!important;margin-left:auto!important}.my-lg-0{margin-top:0!important;margin-bottom:0!important}.my-lg-1{margin-top:.25rem!important;margin-bottom:.25rem!important}.my-lg-2{margin-top:.5rem!important;margin-bottom:.5rem!important}.my-lg-3{margin-top:1rem!important;margin-bottom:1rem!important}.my-lg-4{margin-top:1.5rem!important;margin-bottom:1.5rem!important}.my-lg-5{margin-top:3rem!important;margin-bottom:3rem!important}.my-lg-auto{margin-top:auto!important;margin-bottom:auto!important}.mt-lg-0{margin-top:0!important}.mt-lg-1{margin-top:.25rem!important}.mt-lg-2{margin-top:.5rem!important}.mt-lg-3{margin-top:1rem!important}.mt-lg-4{margin-top:1.5rem!important}.mt-lg-5{margin-top:3rem!important}.mt-lg-auto{margin-top:auto!important}.me-lg-0{margin-right:0!important}.me-lg-1{margin-right:.25rem!important}.me-lg-2{margin-right:.5rem!important}.me-lg-3{margin-right:1rem!important}.me-lg-4{margin-right:1.5rem!important}.me-lg-5{margin-right:3rem!important}.me-lg-auto{margin-right:auto!important}.mb-lg-0{margin-bottom:0!important}.mb-lg-1{margin-bottom:.25rem!important}.mb-lg-2{margin-bottom:.5rem!important}.mb-lg-3{margin-bottom:1rem!important}.mb-lg-4{margin-bottom:1.5rem!important}.mb-lg-5{margin-bottom:3rem!important}.mb-lg-auto{margin-bottom:auto!important}.ms-lg-0{margin-left:0!important}.ms-lg-1{margin-left:.25rem!important}.ms-lg-2{margin-left:.5rem!important}.ms-lg-3{margin-left:1rem!important}.ms-lg-4{margin-left:1.5rem!important}.ms-lg-5{margin-left:3rem!important}.ms-lg-auto{margin-left:auto!important}.p-lg-0{padding:0!important}.p-lg-1{padding:.25rem!important}.p-lg-2{padding:.5rem!important}.p-lg-3{padding:1rem!important}.p-lg-4{padding:1.5rem!important}.p-lg-5{padding:3rem!important}.px-lg-0{padding-right:0!important;padding-left:0!important}.px-lg-1{padding-right:.25rem!important;padding-left:.25rem!important}.px-lg-2{padding-right:.5rem!important;padding-left:.5rem!important}.px-lg-3{padding-right:1rem!important;padding-left:1rem!important}.px-lg-4{padding-right:1.5rem!important;padding-left:1.5rem!important}.px-lg-5{padding-right:3rem!important;padding-left:3rem!important}.py-lg-0{padding-top:0!important;padding-bottom:0!important}.py-lg-1{padding-top:.25rem!important;padding-bottom:.25rem!important}.py-lg-2{padding-top:.5rem!important;padding-bottom:.5rem!important}.py-lg-3{padding-top:1rem!important;padding-bottom:1rem!important}.py-lg-4{padding-top:1.5rem!important;padding-bottom:1.5rem!important}.py-lg-5{padding-top:3rem!important;padding-bottom:3rem!important}.pt-lg-0{padding-top:0!important}.pt-lg-1{padding-top:.25rem!important}.pt-lg-2{padding-top:.5rem!important}.pt-lg-3{padding-top:1rem!important}.pt-lg-4{padding-top:1.5rem!important}.pt-lg-5{padding-top:3rem!important}.pe-lg-0{padding-right:0!important}.pe-lg-1{padding-right:.25rem!important}.pe-lg-2{padding-right:.5rem!important}.pe-lg-3{padding-right:1rem!important}.pe-lg-4{padding-right:1.5rem!important}.pe-lg-5{padding-right:3rem!important}.pb-lg-0{padding-bottom:0!important}.pb-lg-1{padding-bottom:.25rem!important}.pb-lg-2{padding-bottom:.5rem!important}.pb-lg-3{padding-bottom:1rem!important}.pb-lg-4{padding-bottom:1.5rem!important}.pb-lg-5{padding-bottom:3rem!important}.ps-lg-0{padding-left:0!important}.ps-lg-1{padding-left:.25rem!important}.ps-lg-2{padding-left:.5rem!important}.ps-lg-3{padding-left:1rem!important}.ps-lg-4{padding-left:1.5rem!important}.ps-lg-5{padding-left:3rem!important}.text-lg-start{text-align:left!important}.text-lg-end{text-align:right!important}.text-lg-center{text-align:center!important}}@media (min-width:1200px){.float-xl-start{float:left!important}.float-xl-end{float:right!important}.float-xl-none{float:none!important}.d-xl-inline{display:inline!important}.d-xl-inline-block{display:inline-block!important}.d-xl-block{display:block!important}.d-xl-grid{display:grid!important}.d-xl-table{display:table!important}.d-xl-table-row{display:table-row!important}.d-xl-table-cell{display:table-cell!important}.d-xl-flex{display:flex!important}.d-xl-inline-flex{display:inline-flex!important}.d-xl-none{display:none!important}.flex-xl-fill{flex:1 1 auto!important}.flex-xl-row{flex-direction:row!important}.flex-xl-column{flex-direction:column!important}.flex-xl-row-reverse{flex-direction:row-reverse!important}.flex-xl-column-reverse{flex-direction:column-reverse!important}.flex-xl-grow-0{flex-grow:0!important}.flex-xl-grow-1{flex-grow:1!important}.flex-xl-shrink-0{flex-shrink:0!important}.flex-xl-shrink-1{flex-shrink:1!important}.flex-xl-wrap{flex-wrap:wrap!important}.flex-xl-nowrap{flex-wrap:nowrap!important}.flex-xl-wrap-reverse{flex-wrap:wrap-reverse!important}.gap-xl-0{gap:0!important}.gap-xl-1{gap:.25rem!important}.gap-xl-2{gap:.5rem!important}.gap-xl-3{gap:1rem!important}.gap-xl-4{gap:1.5rem!important}.gap-xl-5{gap:3rem!important}.justify-content-xl-start{justify-content:flex-start!important}.justify-content-xl-end{justify-content:flex-end!important}.justify-content-xl-center{justify-content:center!important}.justify-content-xl-between{justify-content:space-between!important}.justify-content-xl-around{justify-content:space-around!important}.justify-content-xl-evenly{justify-content:space-evenly!important}.align-items-xl-start{align-items:flex-start!important}.align-items-xl-end{align-items:flex-end!important}.align-items-xl-center{align-items:center!important}.align-items-xl-baseline{align-items:baseline!important}.align-items-xl-stretch{align-items:stretch!important}.align-content-xl-start{align-content:flex-start!important}.align-content-xl-end{align-content:flex-end!important}.align-content-xl-center{align-content:center!important}.align-content-xl-between{align-content:space-between!important}.align-content-xl-around{align-content:space-around!important}.align-content-xl-stretch{align-content:stretch!important}.align-self-xl-auto{align-self:auto!important}.align-self-xl-start{align-self:flex-start!important}.align-self-xl-end{align-self:flex-end!important}.align-self-xl-center{align-self:center!important}.align-self-xl-baseline{align-self:baseline!important}.align-self-xl-stretch{align-self:stretch!important}.order-xl-first{order:-1!important}.order-xl-0{order:0!important}.order-xl-1{order:1!important}.order-xl-2{order:2!important}.order-xl-3{order:3!important}.order-xl-4{order:4!important}.order-xl-5{order:5!important}.order-xl-last{order:6!important}.m-xl-0{margin:0!important}.m-xl-1{margin:.25rem!important}.m-xl-2{margin:.5rem!important}.m-xl-3{margin:1rem!important}.m-xl-4{margin:1.5rem!important}.m-xl-5{margin:3rem!important}.m-xl-auto{margin:auto!important}.mx-xl-0{margin-right:0!important;margin-left:0!important}.mx-xl-1{margin-right:.25rem!important;margin-left:.25rem!important}.mx-xl-2{margin-right:.5rem!important;margin-left:.5rem!important}.mx-xl-3{margin-right:1rem!important;margin-left:1rem!important}.mx-xl-4{margin-right:1.5rem!important;margin-left:1.5rem!important}.mx-xl-5{margin-right:3rem!important;margin-left:3rem!important}.mx-xl-auto{margin-right:auto!important;margin-left:auto!important}.my-xl-0{margin-top:0!important;margin-bottom:0!important}.my-xl-1{margin-top:.25rem!important;margin-bottom:.25rem!important}.my-xl-2{margin-top:.5rem!important;margin-bottom:.5rem!important}.my-xl-3{margin-top:1rem!important;margin-bottom:1rem!important}.my-xl-4{margin-top:1.5rem!important;margin-bottom:1.5rem!important}.my-xl-5{margin-top:3rem!important;margin-bottom:3rem!important}.my-xl-auto{margin-top:auto!important;margin-bottom:auto!important}.mt-xl-0{margin-top:0!important}.mt-xl-1{margin-top:.25rem!important}.mt-xl-2{margin-top:.5rem!important}.mt-xl-3{margin-top:1rem!important}.mt-xl-4{margin-top:1.5rem!important}.mt-xl-5{margin-top:3rem!important}.mt-xl-auto{margin-top:auto!important}.me-xl-0{margin-right:0!important}.me-xl-1{margin-right:.25rem!important}.me-xl-2{margin-right:.5rem!important}.me-xl-3{margin-right:1rem!important}.me-xl-4{margin-right:1.5rem!important}.me-xl-5{margin-right:3rem!important}.me-xl-auto{margin-right:auto!important}.mb-xl-0{margin-bottom:0!important}.mb-xl-1{margin-bottom:.25rem!important}.mb-xl-2{margin-bottom:.5rem!important}.mb-xl-3{margin-bottom:1rem!important}.mb-xl-4{margin-bottom:1.5rem!important}.mb-xl-5{margin-bottom:3rem!important}.mb-xl-auto{margin-bottom:auto!important}.ms-xl-0{margin-left:0!important}.ms-xl-1{margin-left:.25rem!important}.ms-xl-2{margin-left:.5rem!important}.ms-xl-3{margin-left:1rem!important}.ms-xl-4{margin-left:1.5rem!important}.ms-xl-5{margin-left:3rem!important}.ms-xl-auto{margin-left:auto!important}.p-xl-0{padding:0!important}.p-xl-1{padding:.25rem!important}.p-xl-2{padding:.5rem!important}.p-xl-3{padding:1rem!important}.p-xl-4{padding:1.5rem!important}.p-xl-5{padding:3rem!important}.px-xl-0{padding-right:0!important;padding-left:0!important}.px-xl-1{padding-right:.25rem!important;padding-left:.25rem!important}.px-xl-2{padding-right:.5rem!important;padding-left:.5rem!important}.px-xl-3{padding-right:1rem!important;padding-left:1rem!important}.px-xl-4{padding-right:1.5rem!important;padding-left:1.5rem!important}.px-xl-5{padding-right:3rem!important;padding-left:3rem!important}.py-xl-0{padding-top:0!important;padding-bottom:0!important}.py-xl-1{padding-top:.25rem!important;padding-bottom:.25rem!important}.py-xl-2{padding-top:.5rem!important;padding-bottom:.5rem!important}.py-xl-3{padding-top:1rem!important;padding-bottom:1rem!important}.py-xl-4{padding-top:1.5rem!important;padding-bottom:1.5rem!important}.py-xl-5{padding-top:3rem!important;padding-bottom:3rem!important}.pt-xl-0{padding-top:0!important}.pt-xl-1{padding-top:.25rem!important}.pt-xl-2{padding-top:.5rem!important}.pt-xl-3{padding-top:1rem!important}.pt-xl-4{padding-top:1.5rem!important}.pt-xl-5{padding-top:3rem!important}.pe-xl-0{padding-right:0!important}.pe-xl-1{padding-right:.25rem!important}.pe-xl-2{padding-right:.5rem!important}.pe-xl-3{padding-right:1rem!important}.pe-xl-4{padding-right:1.5rem!important}.pe-xl-5{padding-right:3rem!important}.pb-xl-0{padding-bottom:0!important}.pb-xl-1{padding-bottom:.25rem!important}.pb-xl-2{padding-bottom:.5rem!important}.pb-xl-3{padding-bottom:1rem!important}.pb-xl-4{padding-bottom:1.5rem!important}.pb-xl-5{padding-bottom:3rem!important}.ps-xl-0{padding-left:0!important}.ps-xl-1{padding-left:.25rem!important}.ps-xl-2{padding-left:.5rem!important}.ps-xl-3{padding-left:1rem!important}.ps-xl-4{padding-left:1.5rem!important}.ps-xl-5{padding-left:3rem!important}.text-xl-start{text-align:left!important}.text-xl-end{text-align:right!important}.text-xl-center{text-align:center!important}}@media (min-width:1400px){.float-xxl-start{float:left!important}.float-xxl-end{float:right!important}.float-xxl-none{float:none!important}.d-xxl-inline{display:inline!important}.d-xxl-inline-block{display:inline-block!important}.d-xxl-block{display:block!important}.d-xxl-grid{display:grid!important}.d-xxl-table{display:table!important}.d-xxl-table-row{display:table-row!important}.d-xxl-table-cell{display:table-cell!important}.d-xxl-flex{display:flex!important}.d-xxl-inline-flex{display:inline-flex!important}.d-xxl-none{display:none!important}.flex-xxl-fill{flex:1 1 auto!important}.flex-xxl-row{flex-direction:row!important}.flex-xxl-column{flex-direction:column!important}.flex-xxl-row-reverse{flex-direction:row-reverse!important}.flex-xxl-column-reverse{flex-direction:column-reverse!important}.flex-xxl-grow-0{flex-grow:0!important}.flex-xxl-grow-1{flex-grow:1!important}.flex-xxl-shrink-0{flex-shrink:0!important}.flex-xxl-shrink-1{flex-shrink:1!important}.flex-xxl-wrap{flex-wrap:wrap!important}.flex-xxl-nowrap{flex-wrap:nowrap!important}.flex-xxl-wrap-reverse{flex-wrap:wrap-reverse!important}.gap-xxl-0{gap:0!important}.gap-xxl-1{gap:.25rem!important}.gap-xxl-2{gap:.5rem!important}.gap-xxl-3{gap:1rem!important}.gap-xxl-4{gap:1.5rem!important}.gap-xxl-5{gap:3rem!important}.justify-content-xxl-start{justify-content:flex-start!important}.justify-content-xxl-end{justify-content:flex-end!important}.justify-content-xxl-center{justify-content:center!important}.justify-content-xxl-between{justify-content:space-between!important}.justify-content-xxl-around{justify-content:space-around!important}.justify-content-xxl-evenly{justify-content:space-evenly!important}.align-items-xxl-start{align-items:flex-start!important}.align-items-xxl-end{align-items:flex-end!important}.align-items-xxl-center{align-items:center!important}.align-items-xxl-baseline{align-items:baseline!important}.align-items-xxl-stretch{align-items:stretch!important}.align-content-xxl-start{align-content:flex-start!important}.align-content-xxl-end{align-content:flex-end!important}.align-content-xxl-center{align-content:center!important}.align-content-xxl-between{align-content:space-between!important}.align-content-xxl-around{align-content:space-around!important}.align-content-xxl-stretch{align-content:stretch!important}.align-self-xxl-auto{align-self:auto!important}.align-self-xxl-start{align-self:flex-start!important}.align-self-xxl-end{align-self:flex-end!important}.align-self-xxl-center{align-self:center!important}.align-self-xxl-baseline{align-self:baseline!important}.align-self-xxl-stretch{align-self:stretch!important}.order-xxl-first{order:-1!important}.order-xxl-0{order:0!important}.order-xxl-1{order:1!important}.order-xxl-2{order:2!important}.order-xxl-3{order:3!important}.order-xxl-4{order:4!important}.order-xxl-5{order:5!important}.order-xxl-last{order:6!important}.m-xxl-0{margin:0!important}.m-xxl-1{margin:.25rem!important}.m-xxl-2{margin:.5rem!important}.m-xxl-3{margin:1rem!important}.m-xxl-4{margin:1.5rem!important}.m-xxl-5{margin:3rem!important}.m-xxl-auto{margin:auto!important}.mx-xxl-0{margin-right:0!important;margin-left:0!important}.mx-xxl-1{margin-right:.25rem!important;margin-left:.25rem!important}.mx-xxl-2{margin-right:.5rem!important;margin-left:.5rem!important}.mx-xxl-3{margin-right:1rem!important;margin-left:1rem!important}.mx-xxl-4{margin-right:1.5rem!important;margin-left:1.5rem!important}.mx-xxl-5{margin-right:3rem!important;margin-left:3rem!important}.mx-xxl-auto{margin-right:auto!important;margin-left:auto!important}.my-xxl-0{margin-top:0!important;margin-bottom:0!important}.my-xxl-1{margin-top:.25rem!important;margin-bottom:.25rem!important}.my-xxl-2{margin-top:.5rem!important;margin-bottom:.5rem!important}.my-xxl-3{margin-top:1rem!important;margin-bottom:1rem!important}.my-xxl-4{margin-top:1.5rem!important;margin-bottom:1.5rem!important}.my-xxl-5{margin-top:3rem!important;margin-bottom:3rem!important}.my-xxl-auto{margin-top:auto!important;margin-bottom:auto!important}.mt-xxl-0{margin-top:0!important}.mt-xxl-1{margin-top:.25rem!important}.mt-xxl-2{margin-top:.5rem!important}.mt-xxl-3{margin-top:1rem!important}.mt-xxl-4{margin-top:1.5rem!important}.mt-xxl-5{margin-top:3rem!important}.mt-xxl-auto{margin-top:auto!important}.me-xxl-0{margin-right:0!important}.me-xxl-1{margin-right:.25rem!important}.me-xxl-2{margin-right:.5rem!important}.me-xxl-3{margin-right:1rem!important}.me-xxl-4{margin-right:1.5rem!important}.me-xxl-5{margin-right:3rem!important}.me-xxl-auto{margin-right:auto!important}.mb-xxl-0{margin-bottom:0!important}.mb-xxl-1{margin-bottom:.25rem!important}.mb-xxl-2{margin-bottom:.5rem!important}.mb-xxl-3{margin-bottom:1rem!important}.mb-xxl-4{margin-bottom:1.5rem!important}.mb-xxl-5{margin-bottom:3rem!important}.mb-xxl-auto{margin-bottom:auto!important}.ms-xxl-0{margin-left:0!important}.ms-xxl-1{margin-left:.25rem!important}.ms-xxl-2{margin-left:.5rem!important}.ms-xxl-3{margin-left:1rem!important}.ms-xxl-4{margin-left:1.5rem!important}.ms-xxl-5{margin-left:3rem!important}.ms-xxl-auto{margin-left:auto!important}.p-xxl-0{padding:0!important}.p-xxl-1{padding:.25rem!important}.p-xxl-2{padding:.5rem!important}.p-xxl-3{padding:1rem!important}.p-xxl-4{padding:1.5rem!important}.p-xxl-5{padding:3rem!important}.px-xxl-0{padding-right:0!important;padding-left:0!important}.px-xxl-1{padding-right:.25rem!important;padding-left:.25rem!important}.px-xxl-2{padding-right:.5rem!important;padding-left:.5rem!important}.px-xxl-3{padding-right:1rem!important;padding-left:1rem!important}.px-xxl-4{padding-right:1.5rem!important;padding-left:1.5rem!important}.px-xxl-5{padding-right:3rem!important;padding-left:3rem!important}.py-xxl-0{padding-top:0!important;padding-bottom:0!important}.py-xxl-1{padding-top:.25rem!important;padding-bottom:.25rem!important}.py-xxl-2{padding-top:.5rem!important;padding-bottom:.5rem!important}.py-xxl-3{padding-top:1rem!important;padding-bottom:1rem!important}.py-xxl-4{padding-top:1.5rem!important;padding-bottom:1.5rem!important}.py-xxl-5{padding-top:3rem!important;padding-bottom:3rem!important}.pt-xxl-0{padding-top:0!important}.pt-xxl-1{padding-top:.25rem!important}.pt-xxl-2{padding-top:.5rem!important}.pt-xxl-3{padding-top:1rem!important}.pt-xxl-4{padding-top:1.5rem!important}.pt-xxl-5{padding-top:3rem!important}.pe-xxl-0{padding-right:0!important}.pe-xxl-1{padding-right:.25rem!important}.pe-xxl-2{padding-right:.5rem!important}.pe-xxl-3{padding-right:1rem!important}.pe-xxl-4{padding-right:1.5rem!important}.pe-xxl-5{padding-right:3rem!important}.pb-xxl-0{padding-bottom:0!important}.pb-xxl-1{padding-bottom:.25rem!important}.pb-xxl-2{padding-bottom:.5rem!important}.pb-xxl-3{padding-bottom:1rem!important}.pb-xxl-4{padding-bottom:1.5rem!important}.pb-xxl-5{padding-bottom:3rem!important}.ps-xxl-0{padding-left:0!important}.ps-xxl-1{padding-left:.25rem!important}.ps-xxl-2{padding-left:.5rem!important}.ps-xxl-3{padding-left:1rem!important}.ps-xxl-4{padding-left:1.5rem!important}.ps-xxl-5{padding-left:3rem!important}.text-xxl-start{text-align:left!important}.text-xxl-end{text-align:right!important}.text-xxl-center{text-align:center!important}}@media (min-width:1200px){.fs-1{font-size:2.5rem!important}.fs-2{font-size:2rem!important}.fs-3{font-size:1.75rem!important}.fs-4{font-size:1.5rem!important}}@media print{.d-print-inline{display:inline!important}.d-print-inline-block{display:inline-block!important}.d-print-block{display:block!important}.d-print-grid{display:grid!important}.d-print-table{display:table!important}.d-print-table-row{display:table-row!important}.d-print-table-cell{display:table-cell!important}.d-print-flex{display:flex!important}.d-print-inline-flex{display:inline-flex!important}.d-print-none{display:none!important}}

``

## File: \src\Forum.Blazor\wwwroot\css\auth.css
``css
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
``

## File: \src\Forum.Blazor\wwwroot\css\category.css
``css
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
``

## File: \src\Forum.Blazor\wwwroot\css\create-thread.css
``css
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
``

## File: \src\Forum.Blazor\wwwroot\css\forum-table.css
``css
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
``

## File: \src\Forum.Blazor\wwwroot\css\home.css
``css
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
}
``

## File: \src\Forum.Blazor\wwwroot\css\panel.css
``css
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
``

## File: \src\Forum.Blazor\wwwroot\css\thread-details.css
``css
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
.vote-bar-locked:hover .vote-btn.locked { color: #555e68; background: none; }
``

## File: \src\Forum.Domain\Entities\Vote.cs
``cs
namespace Forum.Domain.Entities;

/// <summary>
/// An upvote (+1) or downvote (-1) cast by a user on a comment.
/// Each user may vote only once per comment (enforced by a unique index).
/// </summary>
public class Vote
{
    public int VoteId { get; set; }
    public int Value { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int CommentId { get; set; }
    public User User { get; set; } = null!;
    public Comment Comment { get; set; } = null!;
}
``

## File: \src\Forum.Infrastructure\Migrations\20260308000611_AddVoteEntity.cs
``cs
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
``

## File: \src\Forum.Infrastructure\Migrations\20260308000611_AddVoteEntity.Designer.cs
``cs
// <auto-generated />
using System;
using Forum.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Forum.Infrastructure.Migrations
{
    [DbContext(typeof(ForumDbContext))]
    [Migration("20260308000611_AddVoteEntity")]
    partial class AddVoteEntity
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
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
``

## File: \src\Forum.Infrastructure\Repositories\VoteRepository.cs
``cs
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
}
``

