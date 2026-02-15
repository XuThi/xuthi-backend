using Cart.Infrastructure.Data;
using Cart.Infrastructure.Entity;
using ProductCatalog.Infrastructure.Data;
using Promotion.Infrastructure.Data;

namespace Cart.Features.SyncCartPrices;

// Command and Result
public record SyncCartPricesCommand(Guid CartId) : ICommand<SyncCartPricesResult>;
public record SyncCartPricesResult(bool Success, CartDto? Cart, List<string>? Warnings);

// Validator
public class SyncCartPricesCommandValidator : AbstractValidator<SyncCartPricesCommand>
{
    public SyncCartPricesCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required");
    }
}

// Handler
internal class SyncCartPricesHandler(CartDbContext cartDb, ProductCatalogDbContext catalogDb, PromotionDbContext promotionDb)
    : ICommandHandler<SyncCartPricesCommand, SyncCartPricesResult>
{
    public async Task<SyncCartPricesResult> Handle(SyncCartPricesCommand cmd, CancellationToken ct)
    {
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new SyncCartPricesResult(false, null, null);

        var warnings = new List<string>();
        var variantIds = cart.Items.Select(i => i.VariantId).ToList();
        
        var variants = await catalogDb.Variants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        foreach (var item in cart.Items.ToList())
        {
            if (!variants.TryGetValue(item.VariantId, out var variant) || variant.IsDeleted)
            {
                warnings.Add($"{item.ProductName} is no longer available");
                cart.Items.Remove(item);
                continue;
            }

            var (unitPrice, compareAtPrice) = await ResolveSalePrice(
                item.ProductId,
                item.VariantId,
                variant.Price,
                ct);

            if (item.UnitPrice != unitPrice)
            {
                warnings.Add($"{item.ProductName} price changed from {item.UnitPrice:N0}đ to {unitPrice:N0}đ");
                item.UnitPrice = unitPrice;
            }

            item.CompareAtPrice = compareAtPrice;
            item.AvailableStock = 10;
            item.IsInStock = true;
            item.UpdatedAt = DateTime.UtcNow;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await cartDb.SaveChangesAsync(ct);

        return new SyncCartPricesResult(true, MapToDto(cart), warnings.Count > 0 ? warnings : null);
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
