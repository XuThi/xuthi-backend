using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Customer.Infrastructure.Data;

public class CustomerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CustomerDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=customer;Username=postgres;Password=postgres");

        return new CustomerDbContext(optionsBuilder.Options);
    }
}
