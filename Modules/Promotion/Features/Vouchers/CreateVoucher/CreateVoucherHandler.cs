using Promotion.Infrastructure.Data;
using Promotion.Infrastructure.Entity;

namespace Promotion.Features.Vouchers.CreateVoucher;

// Request, Command and Result
public record CreateVoucherRequest(
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
    int? MinimumCustomerTier = null,
    bool CanCombineWithOtherVouchers = false,
    bool CanCombineWithSalePrice = true,
    bool FirstPurchaseOnly = false);

public record CreateVoucherCommand(CreateVoucherRequest Request) : ICommand<CreateVoucherResult>;
public record CreateVoucherResult(Guid Id);

// Validator
public class CreateVoucherCommandValidator : AbstractValidator<CreateVoucherCommand>
{
    public CreateVoucherCommandValidator()
    {
        RuleFor(x => x.Request.Code).NotEmpty().WithMessage("Code is required");
        RuleFor(x => x.Request.DiscountValue).GreaterThan(0).WithMessage("DiscountValue must be positive");
        RuleFor(x => x.Request.StartDate).LessThan(x => x.Request.EndDate).WithMessage("StartDate must be before EndDate");
    }
}

// Handler
internal class CreateVoucherHandler(PromotionDbContext db)
    : ICommandHandler<CreateVoucherCommand, CreateVoucherResult>
{
    public async Task<CreateVoucherResult> Handle(CreateVoucherCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        
        // Check for duplicate code
        var exists = await db.Vouchers.AnyAsync(v => v.Code == req.Code, ct);
        if (exists)
            throw new InvalidOperationException($"Voucher code '{req.Code}' already exists");

        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            Code = req.Code.ToUpperInvariant().Trim(),
            Description = req.Description,
            Type = req.Type,
            DiscountValue = req.DiscountValue,
            MinimumOrderAmount = req.MinimumOrderAmount,
            MaximumDiscountAmount = req.MaximumDiscountAmount,
            MaxUsageCount = req.MaxUsageCount,
            MaxUsagePerCustomer = req.MaxUsagePerCustomer,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            ApplicableCategoryId = req.ApplicableCategoryId,
            ApplicableProductIds = req.ApplicableProductIds,
            MinimumCustomerTier = req.MinimumCustomerTier,
            CanCombineWithOtherVouchers = req.CanCombineWithOtherVouchers,
            CanCombineWithSalePrice = req.CanCombineWithSalePrice,
            FirstPurchaseOnly = req.FirstPurchaseOnly,
            IsActive = true
        };

        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync(ct);

        return new CreateVoucherResult(voucher.Id);
    }
}
