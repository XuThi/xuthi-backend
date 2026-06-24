using Cart.Data;
using Cart.ShoppingCarts.Models;
using Cart.ShoppingCarts.Services;

using Core.Caching;
using ProductCatalog.Products.Features.GetCartItemFacts;

namespace Cart.ShoppingCarts.Features.AddItemIntoCart;

public record AddToCartCommand(string? SessionId, Guid? CustomerId, Guid ProductId, Guid VariantId, int Quantity = 1) : ICommand<AddToCartResult>;
public record AddToCartResult(Guid CartId, CartDto Cart);

/// <summary>
/// Add item to cart. Creates cart if doesn't exist.
/// </summary>
internal class AddToCartHandler(
    CartDbContext cartDb,
    ISender sender,
    CartQuoteService quoteService,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<AddToCartCommand, AddToCartResult>
{
    public async Task<AddToCartResult> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        // Get or create cart
        var cart = await GetOrCreateCart(cmd.SessionId, cmd.CustomerId, ct);

        var fact = (await sender.Send(new GetCartItemFactsQuery([cmd.VariantId]), ct))
            .Items
            .FirstOrDefault(i => i.VariantId == cmd.VariantId);

        if (fact is null || !fact.IsAvailable)
            throw new InvalidOperationException($"Variant {cmd.VariantId} not found");

        // Check if item already in cart
        var existingItem = cart.Items.FirstOrDefault(i => i.VariantId == cmd.VariantId);

        if (existingItem is not null)
        {
            // Update quantity with stock validation
            var newQty = existingItem.Quantity + cmd.Quantity;
            if (newQty > fact.StockQuantity)
                throw new InvalidOperationException(
                    $"Không đủ tồn kho. Chỉ còn {fact.StockQuantity} sản phẩm.");

            existingItem.Quantity = newQty;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Validate quantity for new items
            if (cmd.Quantity > fact.StockQuantity)
                throw new InvalidOperationException(
                    $"Không đủ tồn kho. Chỉ còn {fact.StockQuantity} sản phẩm.");

            var newItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = fact.ProductId,
                VariantId = cmd.VariantId,
                ProductName = fact.ProductName,
                VariantSku = fact.VariantSku,
                VariantDescription = fact.VariantDescription,
                ImageUrl = fact.ImageUrl,
                UnitPrice = fact.BasePrice,
                CompareAtPrice = fact.CompareAtPrice,
                Quantity = cmd.Quantity,
                AvailableStock = fact.StockQuantity,
                IsInStock = fact.StockQuantity >= cmd.Quantity
            };
            cartDb.CartItems.Add(newItem);
        }

        var quote = await quoteService.RefreshQuoteAsync(cart, requirePurchasable: false, requireVoucherValid: false, ct);
        cart.UpdatedAt = DateTime.UtcNow;
        await cartDb.SaveChangesAsync(ct);

        // Invalidate cart cache
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return new AddToCartResult(cart.Id, CartMapper.ToDto(cart, quote.WaivesShipping));
    }

    private async Task<ShoppingCart> GetOrCreateCart(string? sessionId, Guid? customerId, CancellationToken ct)
    {
        ShoppingCart? cart = null;

        if (customerId.HasValue)
        {
            cart = await cartDb.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active, ct);
        }
        else if (!string.IsNullOrEmpty(sessionId))
        {
            cart = await cartDb.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.Status == CartStatus.Active, ct);
        }

        if (cart is null)
        {
            cart = new ShoppingCart
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                CustomerId = customerId,
                ExpiresAt = DateTime.UtcNow.AddDays(7) // Cart expires in 7 days
            };
            cartDb.ShoppingCarts.Add(cart);
        }

        return cart;
    }
}
