using Cart.Infrastructure.Data;
using Cart.Infrastructure.Entity;
using ProductCatalog.Infrastructure.Data;

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
internal class SyncCartPricesHandler(CartDbContext cartDb, ProductCatalogDbContext catalogDb)
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

            // Update price
            if (item.UnitPrice != variant.Price)
            {
                warnings.Add($"{item.ProductName} price changed from {item.UnitPrice:N0}đ to {variant.Price:N0}đ");
                item.UnitPrice = variant.Price;
            }

            item.CompareAtPrice = null;
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
}
