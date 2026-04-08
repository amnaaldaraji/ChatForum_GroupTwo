using Forum.Blazor.Components;
using Forum.Blazor.Interfaces;
using Forum.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Enable Razor Components with Interactive Server rendering (SignalR)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Read API base URL from configuration (appsettings.json / appsettings.Production.json)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]!;

// Named HTTP client pointing to the API — used by services that need IHttpClientFactory
builder.Services.AddHttpClient("ForumApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Default scoped HttpClient — injected directly into services that take HttpClient
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl),
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
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();