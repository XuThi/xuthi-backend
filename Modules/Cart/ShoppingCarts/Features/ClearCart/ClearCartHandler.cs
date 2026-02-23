using Cart.Data;

namespace Cart.ShoppingCarts.Features.ClearCart;

// Command and Result
public record ClearCartCommand(Guid CartId) : ICommand<ClearCartResult>;
public record ClearCartResult(bool Success);

// Validator
public class ClearCartCommandValidator : AbstractValidator<ClearCartCommand>
{
    public ClearCartCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required");
    }
}

// Handler
internal class ClearCartHandler(CartDbContext db)
    : ICommandHandler<ClearCartCommand, ClearCartResult>
{
    public async Task<ClearCartResult> Handle(ClearCartCommand cmd, CancellationToken ct)
    {
        var cart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new ClearCartResult(false);

        cart.Items.Clear();
        cart.AppliedVoucherId = null;
        cart.AppliedVoucherCode = null;
        cart.VoucherDiscount = 0;
        cart.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new ClearCartResult(true);
    }
}
