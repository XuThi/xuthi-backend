namespace Promotion.Vouchers.Features.ValidateVoucher;

public record ValidateVoucherQuery(
    string Code, 
    decimal CartTotal, 
    List<Guid>? ProductIds = null,
    Guid? CategoryId = null,
    Guid? CustomerId = null,
    int? CustomerTier = null) 
    : IQuery<ValidateVoucherResult>;

public record ValidateVoucherResult(
    bool IsValid,
    string? ErrorMessage,
    Guid? VoucherId,
    VoucherType? Type,
    decimal DiscountAmount);

internal class ValidateVoucherHandler(PromotionDbContext db)
    : IQueryHandler<ValidateVoucherQuery, ValidateVoucherResult>
{
    public async Task<ValidateVoucherResult> Handle(ValidateVoucherQuery query, CancellationToken ct)
    {
        var voucher = await db.Vouchers
            .FirstOrDefaultAsync(v => v.Code == query.Code.ToUpperInvariant().Trim(), ct);

        if (voucher is null)
            return new ValidateVoucherResult(false, "Mã giảm giá không tồn tại", null, null, 0);

        if (!voucher.IsActive)
            return new ValidateVoucherResult(false, "Mã giảm giá đã bị vô hiệu hóa", null, null, 0);

        var now = DateTime.UtcNow;
        if (now < voucher.StartDate)
            return new ValidateVoucherResult(false, "Mã giảm giá chưa có hiệu lực", null, null, 0);

        if (now > voucher.EndDate)
            return new ValidateVoucherResult(false, "Mã giảm giá đã hết hạn", null, null, 0);

        if (voucher.MaxUsageCount.HasValue && voucher.CurrentUsageCount >= voucher.MaxUsageCount)
            return new ValidateVoucherResult(false, "Mã giảm giá đã hết lượt sử dụng", null, null, 0);

        if (voucher.MinimumOrderAmount.HasValue && query.CartTotal < voucher.MinimumOrderAmount)
            return new ValidateVoucherResult(false, 
                $"Đơn hàng tối thiểu {voucher.MinimumOrderAmount:N0}đ để áp dụng mã này", null, null, 0);

        if (voucher.MinimumCustomerTier.HasValue && query.CustomerTier.HasValue)
        {
            if (query.CustomerTier < voucher.MinimumCustomerTier)
                return new ValidateVoucherResult(false, 
                    $"Mã giảm giá này dành cho khách hàng VIP cấp {voucher.MinimumCustomerTier}+", null, null, 0);
        }

        if (voucher.MaxUsagePerCustomer.HasValue && query.CustomerId.HasValue)
        {
            var customerUsageCount = await db.VoucherUsages
                .CountAsync(u => u.VoucherId == voucher.Id && u.CustomerId == query.CustomerId, ct);
            
            if (customerUsageCount >= voucher.MaxUsagePerCustomer)
                return new ValidateVoucherResult(false, 
                    "Bạn đã sử dụng hết lượt cho mã giảm giá này", null, null, 0);
        }

        if (voucher.ApplicableCategoryId.HasValue && query.CategoryId.HasValue)
        {
            if (query.CategoryId != voucher.ApplicableCategoryId)
                return new ValidateVoucherResult(false, 
                    "Mã giảm giá không áp dụng cho danh mục sản phẩm này", null, null, 0);
        }

        if (voucher.ApplicableProductIds?.Count > 0 && query.ProductIds?.Count > 0)
        {
            var hasApplicableProduct = query.ProductIds.Any(pid => voucher.ApplicableProductIds.Contains(pid));
            if (!hasApplicableProduct)
                return new ValidateVoucherResult(false, 
                    "Mã giảm giá không áp dụng cho sản phẩm trong giỏ hàng", null, null, 0);
        }

        decimal discountAmount = voucher.Type switch
        {
            VoucherType.Percentage => query.CartTotal * (voucher.DiscountValue / 100),
            VoucherType.FixedAmount => voucher.DiscountValue,
            VoucherType.FreeShipping => voucher.DiscountValue,
            _ => 0
        };

        if (voucher.MaximumDiscountAmount.HasValue && discountAmount > voucher.MaximumDiscountAmount)
            discountAmount = voucher.MaximumDiscountAmount.Value;

        if (discountAmount > query.CartTotal)
            discountAmount = query.CartTotal;

        return new ValidateVoucherResult(true, null, voucher.Id, voucher.Type, discountAmount);
    }
}
