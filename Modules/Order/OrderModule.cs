using Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Order.Data;
using Order.Orders.OrderIntake;
using Order.Orders.Services;

namespace Order;

public static class OrderModule
{
    public static IServiceCollection AddOrderModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<OrderDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetPostgresConnectionString("DatabaseConnection"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<OrderDbContext>();

        // Payment service (PayOS)
        builder.Services.AddScoped<IPaymentService, PayOsPaymentService>();
        builder.Services.AddOrderIntake();

        return builder.Services;
    }

    public static WebApplication UseOrderModule(this WebApplication app)
    {
        return app;
    }
}
