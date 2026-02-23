using Cart.Data;
using Cart.ShoppingCarts.Models;

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
