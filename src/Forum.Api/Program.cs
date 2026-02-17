using System.Text;
using Forum.Api.Endpoints;
using Forum.Application;
using Forum.Domain.Entities;
using Forum.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    })
    .AddEntityFrameworkStores<Forum.Infrastructure.Data.ForumDbContext>()
    .AddDefaultTokenProviders();

// Read JWT settings from configuration, falling back to development defaults.
// In production, these values should be provided via environment variables or
// a secure configuration provider (e.g., Azure Key Vault, AWS Secrets Manager).
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGeneration123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ForumApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ForumClient";

// Set JWT Bearer as the default authentication.
// All endpoints requiring authorization will expect a valid JWT in the Authorization header.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Configure token validation parameters to enforce on every incoming request.
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

// Database Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<Forum.Infrastructure.Data.ForumDbContext>();
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Seed roles, admin user, sample categories, threads, and comments.
        Forum.Infrastructure.Data.DbSeeder.SeedAsync(context, userManager, roleManager).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        // Log seeding errors but do not crash the application; the API can
        // still run even if seeding fails (e.g., database already populated).
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazor");

app.UseAuthentication();
app.UseAuthorization();

app.MapCategoryEndpoints();
app.MapThreadEndpoints();
app.MapCommentEndpoints();
app.MapUserEndpoints();
app.MapAuthEndpoints();

app.Run();