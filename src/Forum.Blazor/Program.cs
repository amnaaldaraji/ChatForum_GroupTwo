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
