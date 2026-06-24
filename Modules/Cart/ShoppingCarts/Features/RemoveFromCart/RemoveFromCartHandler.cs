using Cart.Data;
using Cart.ShoppingCarts.Models;
using Cart.ShoppingCarts.Services;

using Core.Caching;

namespace Cart.ShoppingCarts.Features.RemoveFromCart;

// Command and Result
public record RemoveFromCartCommand(Guid CartId, Guid VariantId) : ICommand<RemoveFromCartResult>;
public record RemoveFromCartResult(bool Success, CartDto? Cart);

// Validator
public class RemoveFromCartCommandValidator : AbstractValidator<RemoveFromCartCommand>
{
    public RemoveFromCartCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required");
        RuleFor(x => x.VariantId).NotEmpty().WithMessage("VariantId is required");
    }
}

// Handler
internal class RemoveFromCartHandler(
    CartDbContext db,
    CartQuoteService quoteService,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<RemoveFromCartCommand, RemoveFromCartResult>
{
    public async Task<RemoveFromCartResult> Handle(RemoveFromCartCommand cmd, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId && c.Status == CartStatus.Active, ct);

        if (cart is null)
            return new RemoveFromCartResult(false, null);

        var item = cart.Items.FirstOrDefault(i => i.VariantId == cmd.VariantId);
        CartQuote? quote = null;

        if (item is not null)
        {
            cart.Items.Remove(item);
            quote = await quoteService.RefreshQuoteAsync(cart, requirePurchasable: false, requireVoucherValid: false, ct);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Invalidate cart cache
            cacheInvalidator.Invalidate(CacheKeys.Cart);
        }

        return new RemoveFromCartResult(true, CartMapper.ToDto(cart, quote?.WaivesShipping ?? false));
    }
}
