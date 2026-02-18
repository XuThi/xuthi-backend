using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductCatalog.Features.Media;
using ProductCatalog.Infrastructure;
using ProductCatalog.Infrastructure.Data;

namespace ProductCatalog;

public static class ProductCatalogModule
{
    public static IServiceCollection AddProductCatalogModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext with Aspire PostgreSQL integration
        builder.AddNpgsqlDbContext<ProductCatalogDbContext>("appdata");
        builder.Services.AddScoped<ICloudinaryMediaService, CloudinaryMediaService>();

        return builder.Services;
    }

    public static WebApplication UseProductCatalogModule(this WebApplication app)
    {
        // Seed data in development
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();
            
            // Ensure database is created and apply migrations
            db.Database.EnsureCreated();
            
            // Seed data - runs async but we block for startup
            ProductCatalogSeeder.SeedAsync(app.Services).GetAwaiter().GetResult();
        }
        
        return app;
    }
}
