using Cart.Infrastructure.Data;
using Cart.Infrastructure.Entity;
using ProductCatalog.Infrastructure.Data;

namespace Cart.Features;

// TODO: YES Seperate all of this to different files

/// <summary>
/// Get cart by session ID or customer ID. Creates cart if doesn't exist.
/// </summary>
internal class GetCartHandler(CartDbContext db)
    : IQueryHandler<GetCartQuery, GetCartResult>
{
    public async Task<GetCartResult> Handle(GetCartQuery query, CancellationToken ct)
    {
        ShoppingCart? cart = null;

        if (query.CustomerId.HasValue)
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == query.CustomerId, ct);
        }
        else if (!string.IsNullOrEmpty(query.SessionId))
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == query.SessionId, ct);
        }

        if (cart is null)
            return new GetCartResult(null);

        return new GetCartResult(MapToDto(cart));
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

        // Get variant with product info - include the Product relationship
        var variant = await catalogDb.Variants
            .Include(v => v.OptionSelections)
            .Include(v => v.Product)
                .ThenInclude(p => p.Images)
                    .ThenInclude(pi => pi.Image)
            .FirstOrDefaultAsync(v => v.Id == cmd.VariantId && !v.IsDeleted, ct);

        if (variant is null)
            throw new InvalidOperationException($"Variant {cmd.VariantId} not found");

        // Get product from variant (not from cmd.ProductId which might be wrong)
        var product = variant.Product;

        if (product is null || product.IsDeleted)
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

            cart.Items.Add(new CartItem
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
            });
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

/// <summary>
/// Remove item from cart
/// </summary>
internal class RemoveFromCartHandler(CartDbContext db)
    : ICommandHandler<RemoveFromCartCommand, RemoveFromCartResult>
{
    public async Task<RemoveFromCartResult> Handle(RemoveFromCartCommand cmd, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new RemoveFromCartResult(false, null);

        var item = cart.Items.FirstOrDefault(i => i.VariantId == cmd.VariantId);
        if (item is not null)
        {
            cart.Items.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return new RemoveFromCartResult(true, MapToDto(cart));
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

/// <summary>
/// Clear entire cart
/// </summary>
internal class ClearCartHandler(CartDbContext db)
    : ICommandHandler<ClearCartCommand, ClearCartResult>
{
    public async Task<ClearCartResult> Handle(ClearCartCommand cmd, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new ClearCartResult(false);

        cart.Items.Clear();
        cart.AppliedVoucherId = null;
        cart.AppliedVoucherCode = null;
        cart.VoucherDiscount = 0;
        cart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new ClearCartResult(true);
    }
}
