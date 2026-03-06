using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProductCatalog.Data;

public class ProductCatalogDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProductCatalogDbContext>
{
    public ProductCatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProductCatalogDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=productcatalog;Trusted_Connection=True;TrustServerCertificate=True");

        return new ProductCatalogDbContext(optionsBuilder.Options);
    }
}
