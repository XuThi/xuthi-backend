using Cart.Data;
using Cart.ShoppingCarts.Models;
using Cart.ShoppingCarts.Services;

using Core.Caching;

namespace Cart.ShoppingCarts.Features.ApplyVoucher;

// Command and Result
public record ApplyVoucherCommand(Guid CartId, string VoucherCode) : ICommand<ApplyVoucherResult>;
public record ApplyVoucherResult(bool Success, string? ErrorMessage, decimal DiscountAmount, CartDto? Cart);

// Validator
public class ApplyVoucherCommandValidator : AbstractValidator<ApplyVoucherCommand>
{
    public ApplyVoucherCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required");
        RuleFor(x => x.VoucherCode).NotEmpty().WithMessage("VoucherCode is required");
    }
}

// Handler
internal class ApplyVoucherHandler(
    CartDbContext cartDb,
    CartQuoteService quoteService,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<ApplyVoucherCommand, ApplyVoucherResult>
{
    public async Task<ApplyVoucherResult> Handle(ApplyVoucherCommand cmd, CancellationToken ct)
    {
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId && c.Status == CartStatus.Active, ct);

        if (cart is null)
            return new ApplyVoucherResult(false, "Cart not found", 0, null);

        if (cart.Items.Count == 0)
            return new ApplyVoucherResult(false, "Cart is empty", 0, null);

        var previousVoucherId = cart.AppliedVoucherId;
        var previousVoucherCode = cart.AppliedVoucherCode;
        var previousVoucherDiscount = cart.VoucherDiscount;

        cart.AppliedVoucherCode = cmd.VoucherCode.ToUpperInvariant().Trim();

        CartQuote quote;
        try
        {
            quote = await quoteService.RefreshQuoteAsync(cart, requirePurchasable: false, requireVoucherValid: true, ct);
        }
        catch (InvalidOperationException ex)
        {
            cart.AppliedVoucherId = previousVoucherId;
            cart.AppliedVoucherCode = previousVoucherCode;
            cart.VoucherDiscount = previousVoucherDiscount;
            var restoredQuote = await quoteService.RefreshQuoteAsync(
                cart,
                requirePurchasable: false,
                requireVoucherValid: false,
                ct);

            return new ApplyVoucherResult(false, ex.Message, 0, CartMapper.ToDto(cart, restoredQuote.WaivesShipping));
        }

        cart.UpdatedAt = DateTime.UtcNow;

        await cartDb.SaveChangesAsync(ct);

        // Invalidate cart cache
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return new ApplyVoucherResult(true, null, cart.VoucherDiscount, CartMapper.ToDto(cart, quote.WaivesShipping));
    }
}
