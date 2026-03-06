using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cart.Data;

public class CartDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CartDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=cart;Trusted_Connection=True;TrustServerCertificate=True");

        return new CartDbContext(optionsBuilder.Options);
    }
}
