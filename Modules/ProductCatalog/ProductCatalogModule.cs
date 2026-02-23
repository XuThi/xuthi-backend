using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductCatalog.Data;
using ProductCatalog.Products.Features.Media;

namespace ProductCatalog;

public static class ProductCatalogModule
{
    public static IServiceCollection AddProductCatalogModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<ProductCatalogDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("appdata"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<ProductCatalogDbContext>();
        builder.Services.AddScoped<ICloudinaryMediaService, CloudinaryMediaService>();

        return builder.Services;
    }

    public static WebApplication UseProductCatalogModule(this WebApplication app)
    {
        return app;
    }
}
