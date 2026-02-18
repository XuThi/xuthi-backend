using Cart;
using Cart.Infrastructure.Data;
using Carter;
using Core.Exceptions.Handler;
using Core.Extensions;
using Customer;
using Customer.Infrastructure.Data;
using Identity;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Entity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Order;
using Order.Infrastructure.Data;
using ProductCatalog;
using ProductCatalog.Infrastructure.Data;
using Promotion;
using Promotion.Infrastructure.Data;
using Scalar.AspNetCore;
using System.Text;
using Identity.Infrastructure.Services;
using Promotion.Infrastructure;
using ProductCatalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
// TODO: Remove this add HttpClient
builder.Services.AddHttpClient();

// Add CORS for frontend
// TODO: Remove this when in production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin => 
            origin.StartsWith("http://localhost:") || 
            origin.StartsWith("https://localhost:"))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Add Identity module (DbContext + Identity services)
builder.AddIdentityModule();

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyThatIsAtLeast32Characters!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "XuThiApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "XuThiFrontend";

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
    typeof(IdentityModule).Assembly
);

// Add Carter for minimal API endpoints
builder.Services.AddCarterWithAssemblies(
    typeof(ProductCatalogModule).Assembly,
    typeof(OrderModule).Assembly,
    typeof(PromotionModuleMarker).Assembly,
    typeof(CartModuleMarker).Assembly,
    typeof(CustomerModuleMarker).Assembly,
    typeof(IdentityModule).Assembly
);

// Add modules (DbContext via Aspire - connection strings injected automatically)
builder.AddProductCatalogModule();
builder.AddOrderModule();
builder.AddPromotionModule();
builder.AddCartModule();
builder.AddCustomerModule();

var app = builder.Build();

// Auto-migrate database in development (Aspire ensures DB is ready via WaitFor)
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PromotionDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CartDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    
    // Seed initial product data
    await ProductCatalogSeeder.SeedAsync(app.Services);
    
    // Seed roles and admin user
    await IdentitySeeder.SeedRolesAndAdminAsync(app.Services);

    // Seed promotions/vouchers for testing
    await PromotionSeeder.SeedAsync(app.Services);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Enable CORS
app.UseCors("AllowFrontend");

// Enable authentication/authorization
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Map Carter endpoints from all modules
app.MapCarter();

app.MapDefaultEndpoints();

app.Run();