using Cart.Data;
using Cart.ShoppingCarts.Models;
using Cart.ShoppingCarts.Services;

using Core.Caching;
using ProductCatalog.Products.Features.GetCartItemFacts;

namespace Cart.ShoppingCarts.Features.UpdateCartItem;

public record UpdateCartItemCommand(Guid CartId, Guid VariantId, int Quantity) : ICommand<UpdateCartItemResult>;
public record UpdateCartItemResult(bool Success, CartDto? Cart, string? ErrorMessage);

/// <summary>
/// Update cart item quantity
/// </summary>
internal class UpdateCartItemHandler(
    CartDbContext cartDb,
    ISender sender,
    CartQuoteService quoteService,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<UpdateCartItemCommand, UpdateCartItemResult>
{
    public async Task<UpdateCartItemResult> Handle(UpdateCartItemCommand cmd, CancellationToken ct)
    {
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId && c.Status == CartStatus.Active, ct);

        if (cart is null)
            return new UpdateCartItemResult(false, null, "Cart not found");

        var item = cart.Items.FirstOrDefault(i => i.VariantId == cmd.VariantId);
        if (item is null)
            return new UpdateCartItemResult(false, null, "Item not in cart");

        var fact = (await sender.Send(new GetCartItemFactsQuery([cmd.VariantId]), ct))
            .Items
            .FirstOrDefault(i => i.VariantId == cmd.VariantId);

        if (fact is null || !fact.IsAvailable)
            return new UpdateCartItemResult(false, null, "Variant no longer exists");

        if (cmd.Quantity <= 0)
        {
            // Remove item
            cart.Items.Remove(item);
        }
        else
        {
            if (cmd.Quantity > fact.StockQuantity)
                return new UpdateCartItemResult(false, null, $"Không đủ tồn kho. Chỉ còn {fact.StockQuantity} sản phẩm.");

            item.Quantity = cmd.Quantity;
            item.UpdatedAt = DateTime.UtcNow;
        }

        var quote = await quoteService.RefreshQuoteAsync(cart, requirePurchasable: false, requireVoucherValid: false, ct);
        cart.UpdatedAt = DateTime.UtcNow;
        await cartDb.SaveChangesAsync(ct);

        // Invalidate cart cache
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return new UpdateCartItemResult(true, CartMapper.ToDto(cart, quote.WaivesShipping), null);
    }
}
