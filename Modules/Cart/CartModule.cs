using Cart.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cart;

public static class CartModule
{
    public static IHostApplicationBuilder AddCartModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<CartDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("appdata"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<CartDbContext>();
        return builder;
    }
}
public class CartModuleMarker;
