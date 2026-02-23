using Customer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Customer;

public static class CustomerModule
{
    public static IHostApplicationBuilder AddCustomerModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<CustomerDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("appdata"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<CustomerDbContext>();
        return builder;
    }
}

public class CustomerModuleMarker;
