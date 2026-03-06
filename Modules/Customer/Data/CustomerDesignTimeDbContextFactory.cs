using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Customer.Data;

public class CustomerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CustomerDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=customer;Trusted_Connection=True;TrustServerCertificate=True");

        return new CustomerDbContext(optionsBuilder.Options);
    }
}
