using Cart;
using Cart.Data;
using Carter;
using Core.Exceptions.Handler;
using Core.Extensions;
using Customer;
using Customer.Data;
using Identity;
using Identity.Data;
using Identity.Users.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Order;
using Order.Data;
using ProductCatalog;
using ProductCatalog.Data;
using Promotion;
using Promotion.Data;
using Notification;
using Scalar.AspNetCore;
using System.Text;
using Identity.Users.Services;
using Promotion.Data.Seed;
using ProductCatalog.Data.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? [];

        var isDevelopment = builder.Environment.IsDevelopment();

        policy.SetIsOriginAllowed(origin =>
        {
            // Always allow explicitly configured origins
            if (allowedOrigins.Any(allowed => origin.Equals(allowed, StringComparison.OrdinalIgnoreCase)))
                return true;

            // In development, allow localhost and Vercel preview deploys
            if (isDevelopment)
            {
                return origin.StartsWith("http://localhost:") ||
            origin.StartsWith("https://localhost:") ||
                       origin.EndsWith(".vercel.app");
            }

            // In production, also allow *.vercel.app for preview deployments
            if (origin.EndsWith(".vercel.app"))
                return true;

            return false;
        })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add Identity module (DbContext + Identity services)
builder.AddIdentityModule();

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

var authBuilder = builder.Services.AddAuthentication(options =>
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

// Add Google OAuth only if credentials are configured
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
}

// Add Facebook OAuth only if credentials are configured
var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrEmpty(facebookAppId) && !string.IsNullOrEmpty(facebookAppSecret))
{
    authBuilder.AddFacebook(options =>
    {
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret;
        options.CallbackPath = "/signin-facebook";
    });
}

// Add authorization policies based on Identity roles
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireRole("Admin"))
    .AddPolicy("Staff", policy => policy.RequireRole("Admin", "Staff"))
    .AddPolicy("Customer", policy => policy.RequireAuthenticatedUser())
    .AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add MediatR with all module assemblies
builder.Services.AddMediatRWithAssemblies(
    typeof(ProductCatalogModule).Assembly,
    typeof(OrderModule).Assembly,
    typeof(PromotionModuleMarker).Assembly,
    typeof(CartModuleMarker).Assembly,
    typeof(CustomerModuleMarker).Assembly,
    typeof(IdentityModule).Assembly,
    typeof(NotificationModule).Assembly
);

// Add Carter for minimal API endpoints
builder.Services.AddCarterWithAssemblies(
    typeof(ProductCatalogModule).Assembly,
    typeof(OrderModule).Assembly,
    typeof(PromotionModuleMarker).Assembly,
    typeof(CartModuleMarker).Assembly,
    typeof(CustomerModuleMarker).Assembly,
    typeof(IdentityModule).Assembly,
    typeof(NotificationModule).Assembly
);

// Add modules (DbContext via Aspire - connection strings injected automatically)
builder.AddProductCatalogModule();
builder.AddOrderModule();
builder.AddPromotionModule();
builder.AddCartModule();
builder.AddCustomerModule();
builder.AddNotificationModule();

var app = builder.Build();

// Auto-migrate database (safe for single-instance deployment)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CartDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    
    // Seed roles and admin user (idempotent - safe to run every startup)
    await IdentitySeeder.SeedRolesAndAdminAsync(app.Services);
}

// Seed sample data only in development
if (app.Environment.IsDevelopment())
{
    await ProductCatalogSeeder.SeedAsync(app.Services);
    await PromotionSeeder.SeedAsync(app.Services);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Enable CORS
app.UseCors("AllowFrontend");

// Enable authentication/authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options => options.WithTitle("XuThi API"));
}

// Map Carter endpoints from all modules
app.MapCarter();

app.MapDefaultEndpoints();

app.Run();