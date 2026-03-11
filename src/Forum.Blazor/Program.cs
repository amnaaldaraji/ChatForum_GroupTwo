using Forum.Blazor.Components;
using Forum.Blazor.Interfaces;
using Forum.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Enable Razor Components with Interactive Server rendering (SignalR-based)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Named HTTP client pointing to the API — used by services that need IHttpClientFactory
builder.Services.AddHttpClient("ForumApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5141");
});

// Default scoped HttpClient — injected directly into services that take HttpClient
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5141"),
    Timeout = TimeSpan.FromSeconds(15)
});

// Authentication and authorization services
builder.Services.AddScoped<ITokenStorageService, TokenStorageService>();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
// Register the custom provider as the AuthenticationStateProvider Blazor resolves
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
// Makes the auth state available as a cascading value throughout the component tree
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
    // Use a dedicated error page and enforce HSTS in production
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery(); // Required for Blazor form anti-forgery token validation
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();