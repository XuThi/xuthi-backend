using Cart;
using Carter;
using Core.Exceptions.Handler;
using Core.Extensions;
using Customer;
using Keycloak.AuthServices.Authentication;
using Keycloak.AuthServices.Authorization;
using Microsoft.EntityFrameworkCore;
using Order;
using ProductCatalog;
using Promotion;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

// Add Keycloak authentication
// builder.Services.AddKeycloakWebApiAuthentication(builder.Configuration);

// Add authorization with role-based policies
// builder.Services.AddAuthorizationBuilder()
//     .AddPolicy("Admin", policy => policy.RequireRealmRoles("admin"))
//     .AddPolicy("Staff", policy => policy.RequireRealmRoles("admin", "staff"))
//     .AddPolicy("Customer", policy => policy.RequireRealmRoles("admin", "staff", "customer"))
//     .AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add MediatR with all module assemblies
builder.Services.AddMediatRWithAssemblies(
    typeof(ProductCatalogModule).Assembly,
    typeof(OrderModule).Assembly,
    typeof(PromotionModuleMarker).Assembly,
    typeof(CartModuleMarker).Assembly,
    typeof(CustomerModuleMarker).Assembly
);

// Add Carter for minimal API endpoints
builder.Services.AddCarterWithAssemblies(
    typeof(ProductCatalogModule).Assembly,
    typeof(OrderModule).Assembly,
    typeof(PromotionModuleMarker).Assembly,
    typeof(CartModuleMarker).Assembly,
    typeof(CustomerModuleMarker).Assembly
);

// Add modules (DbContext via Aspire)
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
    
    // Seed initial product data
    await ProductCatalog.Infrastructure.ProductCatalogSeeder.SeedAsync(app.Services);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Add authentication/authorization middleware
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
