using Microsoft.Extensions.Hosting;
using Promotion.Infrastructure.Data;

namespace Promotion;

public static class PromotionModule
{
    public static IHostApplicationBuilder AddPromotionModule(this IHostApplicationBuilder builder)
    {
        // All modules share the same database (monolith)
        builder.AddNpgsqlDbContext<PromotionDbContext>("appdata");
        return builder;
    }
}

// Marker class for assembly scanning
public class PromotionModuleMarker;
