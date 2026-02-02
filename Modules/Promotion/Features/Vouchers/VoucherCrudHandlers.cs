using Promotion.Infrastructure.Data;
using Promotion.Infrastructure.Entity;

namespace Promotion.Features.Vouchers;

// GET all vouchers
internal class GetVouchersHandler(PromotionDbContext db)
    : IQueryHandler<GetVouchersQuery, GetVouchersResult>
{
    public async Task<GetVouchersResult> Handle(GetVouchersQuery query, CancellationToken ct)
    {
        var q = db.Vouchers.AsQueryable();

        if (query.IsActive.HasValue)
            q = q.Where(v => v.IsActive == query.IsActive.Value);

        if (query.ValidOnly == true)
        {
            var now = DateTime.UtcNow;
            q = q.Where(v => v.IsActive
                && v.StartDate <= now
                && v.EndDate >= now
                && (!v.MaxUsageCount.HasValue || v.CurrentUsageCount < v.MaxUsageCount));
        }

        var vouchers = await q
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => MapToDto(v))
            .ToListAsync(ct);

        return new GetVouchersResult(vouchers);
    }

    private static VoucherDto MapToDto(Voucher v) => new(
        v.Id, v.Code, v.Description, v.Type, v.DiscountValue,
        v.MinimumOrderAmount, v.MaximumDiscountAmount,
        v.MaxUsageCount, v.CurrentUsageCount, v.MaxUsagePerCustomer,
        v.StartDate, v.EndDate,
        v.ApplicableCategoryId, v.ApplicableProductIds,
        v.MinimumCustomerTier,
        v.CanCombineWithOtherVouchers, v.CanCombineWithSalePrice, v.FirstPurchaseOnly,
        v.IsActive, v.IsValid);
}

// GET single voucher
internal class GetVoucherHandler(PromotionDbContext db)
    : IQueryHandler<GetVoucherQuery, GetVoucherResult>
{
    public async Task<GetVoucherResult> Handle(GetVoucherQuery query, CancellationToken ct)
    {
        var v = await db.Vouchers.FindAsync([query.Id], ct);
        if (v is null) return new GetVoucherResult(null);

        return new GetVoucherResult(new VoucherDto(
            v.Id, v.Code, v.Description, v.Type, v.DiscountValue,
            v.MinimumOrderAmount, v.MaximumDiscountAmount,
            v.MaxUsageCount, v.CurrentUsageCount, v.MaxUsagePerCustomer,
            v.StartDate, v.EndDate,
            v.ApplicableCategoryId, v.ApplicableProductIds,
            v.MinimumCustomerTier,
            v.CanCombineWithOtherVouchers, v.CanCombineWithSalePrice, v.FirstPurchaseOnly,
            v.IsActive, v.IsValid));
    }
}

// CREATE voucher
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

// UPDATE voucher
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

// DELETE voucher
internal class DeleteVoucherHandler(PromotionDbContext db)
    : ICommandHandler<DeleteVoucherCommand, DeleteVoucherResult>
{
    public async Task<DeleteVoucherResult> Handle(DeleteVoucherCommand cmd, CancellationToken ct)
    {
        var voucher = await db.Vouchers.FindAsync([cmd.Id], ct);
        if (voucher is null)
            return new DeleteVoucherResult(false);

        db.Vouchers.Remove(voucher);
        await db.SaveChangesAsync(ct);
        return new DeleteVoucherResult(true);
    }
}
