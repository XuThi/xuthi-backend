using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductCatalog.Infrastructure.Data;

namespace ProductCatalog;

public static class ProductCatalogModule
{
    public static IServiceCollection AddProductCatalogModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext with Aspire PostgreSQL integration
        // This uses the connection string injected by Aspire AppHost
        builder.AddNpgsqlDbContext<ProductCatalogDbContext>("ProductCatalogDb");

        return builder.Services;
    }

    public static WebApplication UseProductCatalogModule(this WebApplication app)
    {
        // Apply migrations automatically in development
        // For production, you should run migrations separately
        return app;
    }
}
