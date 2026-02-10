using Cart.Infrastructure.Data;
using Cart.Infrastructure.Entity;
using ProductCatalog.Infrastructure.Data;

namespace Cart.Features.AddItemIntoCart;

public record AddToCartCommand(string? SessionId, Guid? CustomerId, Guid ProductId, Guid VariantId, int Quantity = 1) : ICommand<AddToCartResult>;
public record AddToCartResult(Guid CartId, CartDto Cart);

/// <summary>
/// Add item to cart. Creates cart if doesn't exist.
/// </summary>
internal class AddToCartHandler(CartDbContext cartDb, ProductCatalogDbContext catalogDb)
    : ICommandHandler<AddToCartCommand, AddToCartResult>
{
    public async Task<AddToCartResult> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        // Get or create cart
        var cart = await GetOrCreateCart(cmd.SessionId, cmd.CustomerId, ct);

        // Get variant (no Product navigation in BFF design)
        var variant = await catalogDb.Variants
            .Include(v => v.OptionSelections)
            .FirstOrDefaultAsync(v => v.Id == cmd.VariantId && !v.IsDeleted, ct);

        if (variant is null)
            throw new InvalidOperationException($"Variant {cmd.VariantId} not found");

        // Get product using variant.ProductId
        var product = await catalogDb.Products
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .FirstOrDefaultAsync(p => p.Id == variant.ProductId && !p.IsDeleted, ct);

        if (product is null)
            throw new InvalidOperationException($"Product for variant {cmd.VariantId} not found");

        // Check if item already in cart
        var existingItem = cart.Items.FirstOrDefault(i => i.VariantId == cmd.VariantId);

        if (existingItem is not null)
        {
            // Update quantity (no stock check in simplified design)
            existingItem.Quantity += cmd.Quantity;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Add new item
            var imageUrl = product.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Image.Url;

            var variantDesc = string.Join(", ", variant.OptionSelections.Select(os => os.Value));

            var newItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = product.Id,
                VariantId = cmd.VariantId,
                ProductName = product.Name,
                VariantSku = variant.Sku,
                VariantDescription = variantDesc,
                ImageUrl = imageUrl,
                UnitPrice = variant.Price,
                CompareAtPrice = null, // Simplified - no compare price
                Quantity = cmd.Quantity,
                AvailableStock = 10, // Default stock for display
                IsInStock = true // Always in stock in simplified design
            };
            cartDb.CartItems.Add(newItem);
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await cartDb.SaveChangesAsync(ct);

        return new AddToCartResult(cart.Id, MapToDto(cart));
    }

    private async Task<ShoppingCart> GetOrCreateCart(string? sessionId, Guid? customerId, CancellationToken ct)
    {
        ShoppingCart? cart = null;

        if (customerId.HasValue)
        {
            cart = await cartDb.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        }
        else if (!string.IsNullOrEmpty(sessionId))
        {
            cart = await cartDb.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);
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

    private static CartDto MapToDto(ShoppingCart cart) => new(
        cart.Id,
        cart.SessionId,
        cart.CustomerId,
        cart.Items.Select(i => new CartItemDto(
            i.Id, i.ProductId, i.VariantId,
            i.ProductName, i.VariantSku, i.VariantDescription, i.ImageUrl,
            i.UnitPrice, i.CompareAtPrice, i.Quantity, i.TotalPrice,
            i.AvailableStock, i.IsInStock, i.IsOnSale
        )).ToList(),
        cart.Subtotal,
        cart.VoucherDiscount,
        cart.AppliedVoucherCode,
        cart.Total,
        cart.TotalItems
    );
}
