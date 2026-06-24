using Cart.Data;
using Cart.ShoppingCarts.Models;
using Cart.ShoppingCarts.Services;

using Core.Caching;

namespace Cart.ShoppingCarts.Features.RemoveVoucher;

// Command and Result
public record RemoveVoucherCommand(Guid CartId) : ICommand<RemoveVoucherResult>;
public record RemoveVoucherResult(bool Success, CartDto? Cart);

// Validator
public class RemoveVoucherCommandValidator : AbstractValidator<RemoveVoucherCommand>
{
    public RemoveVoucherCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required");
    }
}

// Handler
internal class RemoveVoucherHandler(
    CartDbContext db,
    CartQuoteService quoteService,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<RemoveVoucherCommand, RemoveVoucherResult>
{
    public async Task<RemoveVoucherResult> Handle(RemoveVoucherCommand cmd, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId && c.Status == CartStatus.Active, ct);

        if (cart is null)
            return new RemoveVoucherResult(false, null);

        cart.AppliedVoucherId = null;
        cart.AppliedVoucherCode = null;
        cart.VoucherDiscount = 0;
        var quote = await quoteService.RefreshQuoteAsync(cart, requirePurchasable: false, requireVoucherValid: false, ct);
        cart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // Invalidate cart cache
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return new RemoveVoucherResult(true, CartMapper.ToDto(cart, quote.WaivesShipping));
    }
}
