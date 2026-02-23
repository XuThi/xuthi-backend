namespace Promotion.Vouchers.Features.GetVouchers;

public record GetVouchersQuery(bool? IsActive = null, bool? ValidOnly = null) 
    : IQuery<GetVouchersResult>;
public record GetVouchersResult(List<VoucherDto> Vouchers);

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
