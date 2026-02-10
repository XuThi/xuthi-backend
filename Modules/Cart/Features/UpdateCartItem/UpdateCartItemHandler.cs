using Cart.Infrastructure.Data;
using Cart.Infrastructure.Entity;
using ProductCatalog.Infrastructure.Data;

namespace Cart.Features.UpdateCartItem;

public record UpdateCartItemCommand(Guid CartId, Guid VariantId, int Quantity) : ICommand<UpdateCartItemResult>;
public record UpdateCartItemResult(bool Success, CartDto? Cart, string? ErrorMessage);

/// <summary>
/// Update cart item quantity
/// </summary>
internal class UpdateCartItemHandler(CartDbContext cartDb, ProductCatalogDbContext catalogDb)
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
            item.UnitPrice = variant.Price; // Update to current price
            item.CompareAtPrice = null;
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
}