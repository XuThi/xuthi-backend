using Promotion.Infrastructure.Data;
using Promotion.Infrastructure.Entity;

namespace Promotion.Features.Vouchers.UpdateVoucher;

// Request, Command and Result
public record UpdateVoucherRequest(
    string Code,
    string? Description,
    VoucherType Type,
    decimal DiscountValue,
    decimal? MinimumOrderAmount,
    decimal? MaximumDiscountAmount,
    int? MaxUsageCount,
    int? MaxUsagePerCustomer,
    DateTime StartDate,
    DateTime EndDate,
    Guid? ApplicableCategoryId,
    List<Guid>? ApplicableProductIds,
    int? MinimumCustomerTier,
    bool CanCombineWithOtherVouchers,
    bool CanCombineWithSalePrice,
    bool FirstPurchaseOnly,
    bool IsActive);

public record UpdateVoucherCommand(Guid Id, UpdateVoucherRequest Request) : ICommand<UpdateVoucherResult>;
public record UpdateVoucherResult(bool Success);

// Validator
public class UpdateVoucherCommandValidator : AbstractValidator<UpdateVoucherCommand>
{
    public UpdateVoucherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");
        RuleFor(x => x.Request.Code).NotEmpty().WithMessage("Code is required");
        RuleFor(x => x.Request.DiscountValue).GreaterThan(0).WithMessage("DiscountValue must be positive");
    }
}

// Handler
internal class UpdateVoucherHandler(PromotionDbContext db)
    : ICommandHandler<UpdateVoucherCommand, UpdateVoucherResult>
{
    public async Task<UpdateVoucherResult> Handle(UpdateVoucherCommand cmd, CancellationToken ct)
    {
        var voucher = await db.Vouchers.FindAsync([cmd.Id], ct);
        if (voucher is null)
            return new UpdateVoucherResult(false);

        var req = cmd.Request;
        
        // Check for duplicate code (if changed)
        if (voucher.Code != req.Code)
        {
            var exists = await db.Vouchers.AnyAsync(v => v.Code == req.Code && v.Id != cmd.Id, ct);
            if (exists)
                throw new InvalidOperationException($"Voucher code '{req.Code}' already exists");
        }

        voucher.Code = req.Code.ToUpperInvariant().Trim();
        voucher.Description = req.Description;
        voucher.Type = req.Type;
        voucher.DiscountValue = req.DiscountValue;
        voucher.MinimumOrderAmount = req.MinimumOrderAmount;
        voucher.MaximumDiscountAmount = req.MaximumDiscountAmount;
        voucher.MaxUsageCount = req.MaxUsageCount;
        voucher.MaxUsagePerCustomer = req.MaxUsagePerCustomer;
        voucher.StartDate = req.StartDate;
        voucher.EndDate = req.EndDate;
        voucher.ApplicableCategoryId = req.ApplicableCategoryId;
        voucher.ApplicableProductIds = req.ApplicableProductIds;
        voucher.MinimumCustomerTier = req.MinimumCustomerTier;
        voucher.CanCombineWithOtherVouchers = req.CanCombineWithOtherVouchers;
        voucher.CanCombineWithSalePrice = req.CanCombineWithSalePrice;
        voucher.FirstPurchaseOnly = req.FirstPurchaseOnly;
        voucher.IsActive = req.IsActive;
        voucher.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new UpdateVoucherResult(true);
    }
}
