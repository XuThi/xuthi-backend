using Cart.ShoppingCarts.Services;
using Core.Caching;

namespace Cart.ShoppingCarts.Features.QuoteAndConsumeCart;

public record QuoteCartForCheckoutCommand(Guid CartId, Guid? CustomerId) : ICommand<QuoteCartForCheckoutResult>;

public record QuoteCartForCheckoutResult(CartQuote Quote);

public record ConsumeQuotedCartCommand(Guid CartId, Guid? CustomerId) : ICommand;

public record RestoreConsumedCartCommand(
    Guid CartId,
    Guid? CustomerId,
    IReadOnlyList<RestoreConsumedCartItem> Items,
    Guid? AppliedVoucherId,
    string? AppliedVoucherCode,
    decimal VoucherDiscount) : ICommand;

public record RestoreConsumedCartItem(
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantSku,
    string? VariantDescription,
    string? ImageUrl,
    decimal UnitPrice,
    decimal? CompareAtPrice,
    int Quantity,
    int AvailableStock,
    bool IsInStock);

internal class QuoteCartForCheckoutHandler(
    CartDbContext db,
    CartQuoteService quoteService,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<QuoteCartForCheckoutCommand, QuoteCartForCheckoutResult>
{
    public async Task<QuoteCartForCheckoutResult> Handle(QuoteCartForCheckoutCommand command, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == command.CartId && c.Status == CartStatus.Active, ct);

        if (cart is null)
            throw new InvalidOperationException("Cart not found or already consumed");

        if (cart.CustomerId != command.CustomerId)
            throw new InvalidOperationException("CustomerId does not match the Active Cart");

        if (cart.Items.Count == 0)
            throw new InvalidOperationException("Cart is empty");

        var quote = await quoteService.RefreshQuoteAsync(
            cart,
            requirePurchasable: true,
            requireVoucherValid: true,
            ct);

        cart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return new QuoteCartForCheckoutResult(quote);
    }
}

internal class ConsumeQuotedCartHandler(
    CartDbContext db,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<ConsumeQuotedCartCommand>
{
    public async Task<Unit> Handle(ConsumeQuotedCartCommand command, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c =>
                c.Id == command.CartId &&
                c.CustomerId == command.CustomerId &&
                c.Status == CartStatus.Active,
                ct);

        if (cart is null)
            throw new InvalidOperationException("Cart not found or already consumed");

        cart.Status = CartStatus.Consumed;
        cart.ConsumedAt = DateTime.UtcNow;
        cart.Items.Clear();
        cart.AppliedVoucherId = null;
        cart.AppliedVoucherCode = null;
        cart.VoucherDiscount = 0;
        cart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return Unit.Value;
    }
}

internal class RestoreConsumedCartHandler(
    CartDbContext db,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<RestoreConsumedCartCommand>
{
    public async Task<Unit> Handle(RestoreConsumedCartCommand command, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var cart = await db.ShoppingCarts
            .FirstOrDefaultAsync(c =>
                c.Id == command.CartId &&
                c.CustomerId == command.CustomerId,
                ct);

        if (cart is null)
            throw new InvalidOperationException("Cart not found");

        cart.Status = CartStatus.Active;
        cart.ConsumedAt = null;
        cart.AppliedVoucherId = command.AppliedVoucherId;
        cart.AppliedVoucherCode = command.AppliedVoucherCode;
        cart.VoucherDiscount = command.VoucherDiscount;
        cart.UpdatedAt = DateTime.UtcNow;

        var existingItems = await db.CartItems
            .Where(i => i.CartId == cart.Id)
            .ToListAsync(ct);

        db.CartItems.RemoveRange(existingItems);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        db.CartItems.AddRange(command.Items.Select(item =>
            new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = item.ProductId,
                VariantId = item.VariantId,
                ProductName = item.ProductName,
                VariantSku = item.VariantSku,
                VariantDescription = item.VariantDescription,
                ImageUrl = item.ImageUrl,
                UnitPrice = item.UnitPrice,
                CompareAtPrice = item.CompareAtPrice,
                Quantity = item.Quantity,
                AvailableStock = item.AvailableStock,
                IsInStock = item.IsInStock
            }));

        await db.SaveChangesAsync(ct);
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return Unit.Value;
    }
}
