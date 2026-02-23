using Cart.Data;
using Cart.ShoppingCarts.Models;
using Promotion.Vouchers.Features.ValidateVoucher;

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
internal class ApplyVoucherHandler(CartDbContext cartDb, ISender sender)
    : ICommandHandler<ApplyVoucherCommand, ApplyVoucherResult>
{
    public async Task<ApplyVoucherResult> Handle(ApplyVoucherCommand cmd, CancellationToken ct)
    {
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == cmd.CartId, ct);

        if (cart is null)
            return new ApplyVoucherResult(false, "Cart not found", 0, null);

        if (cart.Items.Count == 0)
            return new ApplyVoucherResult(false, "Cart is empty", 0, null);

        // Validate voucher via Promotion module
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var validateResult = await sender.Send(new ValidateVoucherQuery(
            cmd.VoucherCode,
            cart.Subtotal,
            productIds,
            null,
            cart.CustomerId,
            null
        ), ct);

        if (!validateResult.IsValid)
            return new ApplyVoucherResult(false, validateResult.ErrorMessage, 0, MapToDto(cart));

        // Apply voucher to cart
        cart.AppliedVoucherId = validateResult.VoucherId;
        cart.AppliedVoucherCode = cmd.VoucherCode.ToUpperInvariant().Trim();
        cart.VoucherDiscount = validateResult.DiscountAmount;
        cart.UpdatedAt = DateTime.UtcNow;

        await cartDb.SaveChangesAsync(ct);

        return new ApplyVoucherResult(true, null, validateResult.DiscountAmount, MapToDto(cart));
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
