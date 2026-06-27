using Microsoft.AspNetCore.Builder;
using Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductCatalog.Data;
using ProductCatalog.Products.BackgroundServices;
using ProductCatalog.Products.Features.Media;
using ProductCatalog.Products.Services;

namespace ProductCatalog;

public static class ProductCatalogModule
{
    public static IServiceCollection AddProductCatalogModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<ProductCatalogDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetPostgresConnectionString("DatabaseConnection"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<ProductCatalogDbContext>();
        builder.Services.Configure<StockLifecycleOptions>(
            builder.Configuration.GetSection("ProductCatalog:StockLifecycle"));
        builder.Services.AddScoped<ICloudinaryMediaService, CloudinaryMediaService>();
        builder.Services.AddScoped<IStockReservationService, StockReservationService>();
        builder.Services.AddHostedService<StockReservationCleanupService>();

        return builder.Services;
    }

    public static WebApplication UseProductCatalogModule(this WebApplication app)
    {
        return app;
    }
}
