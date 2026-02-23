using Cart.Data;
using Cart.ShoppingCarts.Models;

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
internal class GetCartHandler(CartDbContext db)
    : IQueryHandler<GetCartQuery, GetCartResult>
{
    public async Task<GetCartResult> Handle(GetCartQuery query, CancellationToken ct)
    {
        ShoppingCart? cart = null;

        if (query.CartId.HasValue)
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == query.CartId, ct);
        }

        if (cart == null && query.CustomerId.HasValue)
        {
            cart = await db.ShoppingCarts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == query.CustomerId, ct);
        }
        else if (cart == null && !string.IsNullOrEmpty(query.SessionId))
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
        [.. cart.Items.Select(i => new CartItemDto(
            i.Id, i.ProductId, i.VariantId,
            i.ProductName, i.VariantSku, i.VariantDescription, i.ImageUrl,
            i.UnitPrice, i.CompareAtPrice, i.Quantity, i.TotalPrice,
            i.AvailableStock, i.IsInStock, i.IsOnSale
        ))],
        cart.Subtotal,
        cart.VoucherDiscount,
        cart.AppliedVoucherCode,
        cart.Total,
        cart.TotalItems
    );
}
