using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Promotion.Data;

public class PromotionDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PromotionDbContext>
{
    public PromotionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PromotionDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=promotion;Trusted_Connection=True;TrustServerCertificate=True");

        return new PromotionDbContext(optionsBuilder.Options);
    }
}
