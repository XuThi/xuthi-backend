using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Order.Data;

public class OrderDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=orders;Trusted_Connection=True;TrustServerCertificate=True");

        return new OrderDbContext(optionsBuilder.Options);
    }
}
