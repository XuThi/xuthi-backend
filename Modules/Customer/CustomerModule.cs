using Customer.Infrastructure.Data;
using Microsoft.Extensions.Hosting;

namespace Customer;

public static class CustomerModule
{
    public static IHostApplicationBuilder AddCustomerModule(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<CustomerDbContext>("appdata");
        return builder;
    }
}

public class CustomerModuleMarker;
