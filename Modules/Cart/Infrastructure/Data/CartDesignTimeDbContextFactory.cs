using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cart.Infrastructure.Data;

// TODO: I will do something with this later

public class CartDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CartDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=cart;Username=postgres;Password=postgres");

        return new CartDbContext(optionsBuilder.Options);
    }
}
