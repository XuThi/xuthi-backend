using Cart.Data;
using Cart.ShoppingCarts.Models;
using Cart.ShoppingCarts.Services;

namespace Cart.ShoppingCarts.Features.GetCart;

public record GetCartQuery(string? SessionId, Guid? CustomerId, Guid? CartId = null) : IQuery<GetCartResult>;
public record GetCartResult(CartDto? Cart);

public class GetCartQueryValidator : AbstractValidator<GetCartQuery>
{
    public GetCartQueryValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrEmpty(x.SessionId) || x.CustomerId.HasValue || x.CartId.HasValue)
            .WithMessage("Either SessionId, CustomerId or CartId must be provided");
    }
}

/// <summary>
/// Get cart by session ID, customer ID, or cart ID.
/// </summary>
internal class GetCartHandler(CartDbContext db, CartQuoteService quoteService)
    : IQueryHandler<GetCartQuery, GetCartResult>
{
    public async Task<GetCartResult> Handle(GetCartQuery query, CancellationToken ct)
    {
        ShoppingCart? cart = null;

        if (query.CartId.HasValue)
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == query.CartId && c.Status == CartStatus.Active, ct);
        }

        if (cart == null && query.CustomerId.HasValue)
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == query.CustomerId && c.Status == CartStatus.Active, ct);
        }
        else if (cart == null && !string.IsNullOrEmpty(query.SessionId))
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == query.SessionId && c.Status == CartStatus.Active, ct);
        }

        if (cart is null)
            return new GetCartResult(null);

        var quote = await quoteService.RefreshQuoteAsync(cart, requirePurchasable: false, requireVoucherValid: false, ct);
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new GetCartResult(CartMapper.ToDto(cart, quote.WaivesShipping));
    }
}
