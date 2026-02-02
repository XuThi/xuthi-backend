using Cart.Infrastructure.Data;
using Cart.Infrastructure.Entity;
using ProductCatalog.Infrastructure.Data;
using Promotion.Features.Vouchers;

namespace Cart.Features;

// TODO: Same still need to extract all of these

/// <summary>
/// Apply voucher to cart. Validates with Promotion module.
/// </summary>
internal class ApplyVoucherHandler(CartDbContext cartDb, ISender sender)
    : ICommandHandler<ApplyVoucherCommand, ApplyVoucherResult>
{
    public async Task<ApplyVoucherResult> Handle(ApplyVoucherCommand cmd, CancellationToken ct)
    {
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new ApplyVoucherResult(false, "Cart not found", 0, null);

        if (cart.Items.Count == 0)
            return new ApplyVoucherResult(false, "Cart is empty", 0, null);

        // Validate voucher via Promotion module
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var validateResult = await sender.Send(new ValidateVoucherQuery(
            cmd.VoucherCode,
            cart.Subtotal,
            productIds,
            null, // CategoryId - could be enhanced
            cart.CustomerId,
            null // CustomerTier - needs Customer module
        ), ct);

        if (!validateResult.IsValid)
            return new ApplyVoucherResult(false, validateResult.ErrorMessage, 0, MapToDto(cart));

        // Apply voucher to cart
        cart.AppliedVoucherId = validateResult.VoucherId;
        cart.AppliedVoucherCode = cmd.VoucherCode.ToUpperInvariant().Trim();
        cart.VoucherDiscount = validateResult.DiscountAmount;
        cart.UpdatedAt = DateTime.UtcNow;

        await cartDb.SaveChangesAsync(ct);

        return new ApplyVoucherResult(true, null, validateResult.DiscountAmount, MapToDto(cart));
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
/// Remove voucher from cart
/// </summary>
internal class RemoveVoucherHandler(CartDbContext db)
    : ICommandHandler<RemoveVoucherCommand, RemoveVoucherResult>
{
    public async Task<RemoveVoucherResult> Handle(RemoveVoucherCommand cmd, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new RemoveVoucherResult(false, null);

        cart.AppliedVoucherId = null;
        cart.AppliedVoucherCode = null;
        cart.VoucherDiscount = 0;
        cart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new RemoveVoucherResult(true, MapToDto(cart));
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
/// Sync cart prices with ProductCatalog. Call before checkout.
/// </summary>
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

            item.CompareAtPrice = null; // No compare price in simplified design
            item.AvailableStock = 10; // Default stock
            item.IsInStock = true; // Always in stock

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

/// <summary>
/// Merge anonymous cart into customer cart (after login)
/// </summary>
internal class MergeCartsHandler(CartDbContext db)
    : ICommandHandler<MergeCartsCommand, MergeCartsResult>
{
    public async Task<MergeCartsResult> Handle(MergeCartsCommand cmd, CancellationToken ct)
    {
        var anonymousCart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == cmd.SessionId, ct);

        if (anonymousCart is null || anonymousCart.Items.Count == 0)
            return new MergeCartsResult(false, null);

        var customerCart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == cmd.CustomerId, ct);

        if (customerCart is null)
        {
            // Just transfer the anonymous cart to the customer
            anonymousCart.CustomerId = cmd.CustomerId;
            anonymousCart.SessionId = null;
            anonymousCart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new MergeCartsResult(true, MapToDto(anonymousCart));
        }

        // Merge items from anonymous cart into customer cart
        foreach (var anonItem in anonymousCart.Items)
        {
            var existingItem = customerCart.Items.FirstOrDefault(i => i.VariantId == anonItem.VariantId);
            if (existingItem is not null)
            {
                // Add quantities
                existingItem.Quantity += anonItem.Quantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Move item to customer cart
                anonItem.CartId = customerCart.Id;
                customerCart.Items.Add(anonItem);
            }
        }

        // Transfer voucher if customer cart doesn't have one
        if (!customerCart.AppliedVoucherId.HasValue && anonymousCart.AppliedVoucherId.HasValue)
        {
            customerCart.AppliedVoucherId = anonymousCart.AppliedVoucherId;
            customerCart.AppliedVoucherCode = anonymousCart.AppliedVoucherCode;
            customerCart.VoucherDiscount = anonymousCart.VoucherDiscount;
        }

        customerCart.UpdatedAt = DateTime.UtcNow;

        // Delete anonymous cart
        db.ShoppingCarts.Remove(anonymousCart);
        
        await db.SaveChangesAsync(ct);

        return new MergeCartsResult(true, MapToDto(customerCart));
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
