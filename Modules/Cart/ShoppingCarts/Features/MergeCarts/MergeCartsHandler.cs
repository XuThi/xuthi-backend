using Cart.Data;
using Cart.ShoppingCarts.Models;

namespace Cart.ShoppingCarts.Features.MergeCarts;

public record MergeCartsCommand(string SessionId, Guid CustomerId) : ICommand<MergeCartsResult>;
public record MergeCartsResult(bool Success, CartDto? Cart);

public class MergeCartsCommandValidator : AbstractValidator<MergeCartsCommand>
{
    public MergeCartsCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required");
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("CustomerId is required");
    }
}

internal class MergeCartsHandler(CartDbContext db)
    : ICommandHandler<MergeCartsCommand, MergeCartsResult>
{
    public async Task<MergeCartsResult> Handle(MergeCartsCommand cmd, CancellationToken ct)
    {
        var anonymousCart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == cmd.SessionId, ct);

        if (anonymousCart is null || anonymousCart.Items.Count == 0)
            return new MergeCartsResult(false, null);

        var customerCart = await db.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == cmd.CustomerId, ct);

        if (customerCart is null)
        {
            // Just transfer the anonymous cart to the customer
            anonymousCart.CustomerId = cmd.CustomerId;
            anonymousCart.SessionId = null;
            anonymousCart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new MergeCartsResult(true, MapToDto(anonymousCart));
        }

        // Merge items from anonymous cart into customer cart
        foreach (var anonItem in anonymousCart.Items)
        {
            var existingItem = customerCart.Items.FirstOrDefault(i => i.VariantId == anonItem.VariantId);
            if (existingItem is not null)
            {
                // Add quantities
                existingItem.Quantity += anonItem.Quantity;
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Move item to customer cart
                anonItem.CartId = customerCart.Id;
                customerCart.Items.Add(anonItem);
            }
        }

        // Transfer voucher if customer cart doesn't have one
        if (!customerCart.AppliedVoucherId.HasValue && anonymousCart.AppliedVoucherId.HasValue)
        {
            customerCart.AppliedVoucherId = anonymousCart.AppliedVoucherId;
            customerCart.AppliedVoucherCode = anonymousCart.AppliedVoucherCode;
            customerCart.VoucherDiscount = anonymousCart.VoucherDiscount;
        }

        customerCart.UpdatedAt = DateTime.UtcNow;

        // Delete anonymous cart
        db.ShoppingCarts.Remove(anonymousCart);

        await db.SaveChangesAsync(ct);

        return new MergeCartsResult(true, MapToDto(customerCart));
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
