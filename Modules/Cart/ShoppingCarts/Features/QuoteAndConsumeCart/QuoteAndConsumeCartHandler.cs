using Cart.ShoppingCarts.Services;
using Core.Caching;

namespace Cart.ShoppingCarts.Features.QuoteAndConsumeCart;

public record QuoteCartForCheckoutCommand(Guid CartId, Guid? CustomerId) : ICommand<QuoteCartForCheckoutResult>;

public record QuoteCartForCheckoutResult(CartQuote Quote);

public record ConsumeQuotedCartCommand(Guid CartId, Guid? CustomerId) : ICommand;

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
