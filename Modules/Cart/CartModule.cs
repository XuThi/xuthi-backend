using Cart.Infrastructure.Data;
using Microsoft.Extensions.Hosting;

namespace Cart;

public static class CartModule
{
    public static IHostApplicationBuilder AddCartModule(this IHostApplicationBuilder builder)
    {
        // i will dealt with this hardcode database later
        builder.AddNpgsqlDbContext<CartDbContext>("appdata");
        return builder;
    }
}
public class CartModuleMarker;
