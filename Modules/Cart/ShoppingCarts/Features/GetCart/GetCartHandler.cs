using Cart.Data;
using Cart.ShoppingCarts.Models;
using Microsoft.Extensions.Caching.Memory;
using Core.Caching;

namespace Cart.ShoppingCarts.Features.GetCart;

public record GetCartQuery(string? SessionId, Guid? CustomerId, Guid? CartId = null) : IQuery<GetCartResult>;
public record GetCartResult(CartDto? Cart);

public class GetCartQueryValidator : AbstractValidator<GetCartQuery>
{
    public GetCartQueryValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrEmpty(x.SessionId) || x.CustomerId.HasValue || x.CartId.HasValue)
            .WithMessage("Either SessionId, CustomerId or CartId must be provided");
    }
}

/// <summary>
/// Get cart by session ID, customer ID, or cart ID with in-memory caching.
/// </summary>
internal class GetCartHandler(CartDbContext db, IMemoryCache cache, ICacheInvalidator cacheInvalidator)
    : IQueryHandler<GetCartQuery, GetCartResult>
{
    public async Task<GetCartResult> Handle(GetCartQuery query, CancellationToken ct)
    {
        var cacheKey = BuildCacheKey(query);
        
        if (cache.TryGetValue(cacheKey, out GetCartResult? cachedResult))
        {
            return cachedResult!;
        }

        ShoppingCart? cart = null;

        if (query.CartId.HasValue)
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == query.CartId, ct);
        }

        if (cart == null && query.CustomerId.HasValue)
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == query.CustomerId, ct);
        }
        else if (cart == null && !string.IsNullOrEmpty(query.SessionId))
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == query.SessionId, ct);
        }

        var result = new GetCartResult(cart is null ? null : MapToDto(cart));

        // Cache for 5 minutes, track key for invalidation
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
        
        cache.Set(cacheKey, result, cacheOptions);
        cacheInvalidator.TrackKey(cacheKey);

        return result;
    }

    private static string BuildCacheKey(GetCartQuery query)
    {
        if (query.CartId.HasValue) return CacheKeys.Build(CacheKeys.Cart, $"id={query.CartId}");
        if (query.CustomerId.HasValue) return CacheKeys.Build(CacheKeys.Cart, $"customer={query.CustomerId}");
        return CacheKeys.Build(CacheKeys.Cart, $"session={query.SessionId}");
    }

    private static CartDto MapToDto(ShoppingCart cart) => new(
        cart.Id,
        cart.SessionId,
        cart.CustomerId,
        [.. cart.Items.Select(i => new CartItemDto(
            i.Id, i.ProductId, i.VariantId,
            i.ProductName, i.VariantSku, i.VariantDescription, i.ImageUrl,
            i.UnitPrice, i.CompareAtPrice, i.Quantity, i.TotalPrice,
            i.AvailableStock, i.IsInStock, i.IsOnSale
        ))],
        cart.Subtotal,
        cart.VoucherDiscount,
        cart.AppliedVoucherCode,
        cart.Total,
        cart.TotalItems
    );
}
