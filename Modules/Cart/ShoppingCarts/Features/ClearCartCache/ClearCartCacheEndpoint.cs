using Core.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Cart.ShoppingCarts.Features.ClearCartCache;

public class ClearCartCacheEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/carts/clear-cache", (ICacheInvalidator cacheInvalidator) =>
        {
            cacheInvalidator.Invalidate(CacheKeys.Cart);
            return Results.Ok(new { Success = true, Message = "Cart cache cleared" });
        })
        .WithName("ClearCartCache")
        .RequireAuthorization("Admin") // Only admins can clear cache
        .WithTags("Cart");
    }
}
