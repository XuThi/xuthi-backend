using Customer.Infrastructure.Data;
using Microsoft.Extensions.Hosting;

namespace Customer;

// TODO: Refactor this

public static class CustomerModule
{
    public static IHostApplicationBuilder AddCustomerModule(this IHostApplicationBuilder builder)
    {
        // All modules share the same database (monolith)
        builder.AddNpgsqlDbContext<CustomerDbContext>("appdata");
        return builder;
    }
}

public class CustomerModuleMarker;
