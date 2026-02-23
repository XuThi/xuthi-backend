using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Promotion.Data;

public class PromotionDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PromotionDbContext>
{
    public PromotionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PromotionDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=promotion;Username=postgres;Password=postgres");

        return new PromotionDbContext(optionsBuilder.Options);
    }
}
