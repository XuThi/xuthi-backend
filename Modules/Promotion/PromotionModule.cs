using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Promotion.Data;

namespace Promotion;

public static class PromotionModule
{
    public static IHostApplicationBuilder AddPromotionModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<PromotionDbContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("DatabaseConnection"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichSqlServerDbContext<PromotionDbContext>();
        return builder;
    }
}

// Marker class for assembly scanning
public class PromotionModuleMarker;
