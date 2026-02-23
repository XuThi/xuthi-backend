using Cart.Data;
using Cart.ShoppingCarts.Models;

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
