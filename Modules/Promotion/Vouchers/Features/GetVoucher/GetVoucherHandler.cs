namespace Promotion.Vouchers.Features.GetVoucher;

public record GetVoucherQuery(Guid Id) : IQuery<GetVoucherResult>;
public record GetVoucherResult(VoucherDto? Voucher);

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
