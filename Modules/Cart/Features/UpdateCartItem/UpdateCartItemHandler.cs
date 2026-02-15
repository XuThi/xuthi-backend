using Cart.Infrastructure.Data;
using Cart.Infrastructure.Entity;
using ProductCatalog.Infrastructure.Data;
using Promotion.Infrastructure.Data;

namespace Cart.Features.UpdateCartItem;

public record UpdateCartItemCommand(Guid CartId, Guid VariantId, int Quantity) : ICommand<UpdateCartItemResult>;
public record UpdateCartItemResult(bool Success, CartDto? Cart, string? ErrorMessage);

/// <summary>
/// Update cart item quantity
/// </summary>
internal class UpdateCartItemHandler(CartDbContext cartDb, ProductCatalogDbContext catalogDb, PromotionDbContext promotionDb)
    : ICommandHandler<UpdateCartItemCommand, UpdateCartItemResult>
{
    public async Task<UpdateCartItemResult> Handle(UpdateCartItemCommand cmd, CancellationToken ct)
    {
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new UpdateCartItemResult(false, null, "Cart not found");

        var item = cart.Items.FirstOrDefault(i => i.VariantId == cmd.VariantId);
        if (item is null)
            return new UpdateCartItemResult(false, null, "Item not in cart");

        // Get variant for current price
        var variant = await catalogDb.Variants
            .FirstOrDefaultAsync(v => v.Id == cmd.VariantId && !v.IsDeleted, ct);

        if (variant is null)
            return new UpdateCartItemResult(false, null, "Variant no longer exists");

        if (cmd.Quantity <= 0)
        {
            // Remove item
            cart.Items.Remove(item);
        }
        else
        {
            item.Quantity = cmd.Quantity;
            item.AvailableStock = 10; // Default stock
            var (unitPrice, compareAtPrice) = await ResolveSalePrice(
                item.ProductId,
                item.VariantId,
                variant.Price,
                ct);
            item.UnitPrice = unitPrice;
            item.CompareAtPrice = compareAtPrice;
            item.UpdatedAt = DateTime.UtcNow;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await cartDb.SaveChangesAsync(ct);

        return new UpdateCartItemResult(true, MapToDto(cart), null);
    }

    private static CartDto MapToDto(ShoppingCart cart) => new(
        cart.Id, cart.SessionId, cart.CustomerId,
        cart.Items.Select(i => new CartItemDto(
            i.Id, i.ProductId, i.VariantId,
            i.ProductName, i.VariantSku, i.VariantDescription, i.ImageUrl,
            i.UnitPrice, i.CompareAtPrice, i.Quantity, i.TotalPrice,
            i.AvailableStock, i.IsInStock, i.IsOnSale
        )).ToList(),
        cart.Subtotal, cart.VoucherDiscount, cart.AppliedVoucherCode, cart.Total, cart.TotalItems
    );

    private async Task<(decimal UnitPrice, decimal? CompareAtPrice)> ResolveSalePrice(
        Guid productId,
        Guid variantId,
        decimal basePrice,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var saleItem = await promotionDb.SaleCampaignItems
            .Include(i => i.SaleCampaign)
            .Where(i => i.ProductId == productId && (i.VariantId == null || i.VariantId == variantId))
            .Where(i => i.SaleCampaign.IsActive && i.SaleCampaign.StartDate <= now && i.SaleCampaign.EndDate >= now)
            .OrderByDescending(i => i.VariantId.HasValue)
            .ThenBy(i => i.SalePrice)
            .FirstOrDefaultAsync(ct);

        if (saleItem is null)
        {
            return (basePrice, null);
        }

        var original = saleItem.OriginalPrice ?? basePrice;
        if (original < saleItem.SalePrice)
        {
            original = basePrice;
        }

        return (saleItem.SalePrice, original);
    }
}