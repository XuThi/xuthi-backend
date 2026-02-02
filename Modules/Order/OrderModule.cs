using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Promotion.Infrastructure.Data;

namespace Order;

public static class OrderModule
{
    public static IServiceCollection AddOrderModule(this IHostApplicationBuilder builder)
    {
        // All modules share the same database (monolith)
        builder.AddNpgsqlDbContext<OrderDbContext>("appdata");
        return builder.Services;
    }

    public static WebApplication UseOrderModule(this WebApplication app)
    {
        return app;
    }
}
