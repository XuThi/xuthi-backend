using Cart.Data;
using Cart.ShoppingCarts.Models;
using Cart.ShoppingCarts.Services;

using Core.Caching;

namespace Cart.ShoppingCarts.Features.SyncCartPrices;

// Command and Result
public record SyncCartPricesCommand(Guid CartId) : ICommand<SyncCartPricesResult>;
public record SyncCartPricesResult(bool Success, CartDto? Cart, List<string>? Warnings);

// Validator
public class SyncCartPricesCommandValidator : AbstractValidator<SyncCartPricesCommand>
{
    public SyncCartPricesCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required");
    }
}

// Handler
internal class SyncCartPricesHandler(
    CartDbContext cartDb,
    CartQuoteService quoteService,
    ICacheInvalidator cacheInvalidator)
    : ICommandHandler<SyncCartPricesCommand, SyncCartPricesResult>
{
    public async Task<SyncCartPricesResult> Handle(SyncCartPricesCommand cmd, CancellationToken ct)
    {
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId && c.Status == CartStatus.Active, ct);

        if (cart is null)
            return new SyncCartPricesResult(false, null, null);

        var previousPrices = cart.Items.ToDictionary(i => i.VariantId, i => i.UnitPrice);
        var quote = await quoteService.RefreshQuoteAsync(cart, requirePurchasable: false, requireVoucherValid: false, ct);
        var warnings = quote.Warnings.ToList();

        foreach (var item in cart.Items)
        {
            if (previousPrices.TryGetValue(item.VariantId, out var previousPrice) && previousPrice != item.UnitPrice)
                warnings.Add($"{item.ProductName} price changed from {previousPrice:N0}đ to {item.UnitPrice:N0}đ");
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await cartDb.SaveChangesAsync(ct);

        // Invalidate cart cache
        cacheInvalidator.Invalidate(CacheKeys.Cart);

        return new SyncCartPricesResult(true, CartMapper.ToDto(cart, quote.WaivesShipping), warnings.Count > 0 ? warnings : null);
    }
}
