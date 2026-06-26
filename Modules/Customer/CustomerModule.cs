using Customer.Data;
using Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Customer;

// TODO: Deal with this shit
public static class CustomerModule
{
    public static IHostApplicationBuilder AddCustomerModule(this IHostApplicationBuilder builder)
    {
        // Add DbContext (non-pooled) so scoped DispatchDomainEventsInterceptor can be resolved
        builder.Services.AddDbContext<CustomerDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetPostgresConnectionString("DatabaseConnection"));
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
        builder.EnrichNpgsqlDbContext<CustomerDbContext>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<Customers.Features.RecordCustomerOrderOutcome.CustomerLoyaltyOutcomeRecorder>();
        return builder;
    }
}

public class CustomerModuleMarker;
