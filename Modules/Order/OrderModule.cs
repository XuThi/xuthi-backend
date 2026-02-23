using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Order.Data;

namespace Order;

public static class OrderModule
{
    public static IServiceCollection AddOrderModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<OrderDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("appdata"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<OrderDbContext>();
        return builder.Services;
    }

    public static WebApplication UseOrderModule(this WebApplication app)
    {
        return app;
    }
}
