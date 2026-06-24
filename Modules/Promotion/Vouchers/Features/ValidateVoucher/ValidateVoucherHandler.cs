namespace Promotion.Vouchers.Features.ValidateVoucher;

public record ValidateVoucherQuery(
    string Code, 
    decimal CartQuoteAmount,
    List<Guid>? ProductIds = null,
    Guid? CategoryId = null,
    Guid? CustomerId = null,
    int? CustomerTier = null,
    int? CustomerTotalOrders = null,
    List<VoucherValidationLine>? Lines = null)
    : IQuery<ValidateVoucherResult>;

public record VoucherValidationLine(
    Guid ProductId,
    Guid CategoryId,
    decimal LineTotal,
    bool IsOnSale);

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

        if (voucher.MinimumOrderAmount.HasValue && query.CartQuoteAmount < voucher.MinimumOrderAmount)
            return new ValidateVoucherResult(false, 
                $"Đơn hàng tối thiểu {voucher.MinimumOrderAmount:N0}đ để áp dụng mã này", null, null, 0);

        if (voucher.MinimumCustomerTier.HasValue)
        {
            if (!query.CustomerTier.HasValue || query.CustomerTier < voucher.MinimumCustomerTier)
                return new ValidateVoucherResult(false, 
                    $"Mã giảm giá này dành cho hạng khách hàng {voucher.MinimumCustomerTier}+", null, null, 0);
        }

        if (voucher.MaxUsagePerCustomer.HasValue)
        {
            if (!query.CustomerId.HasValue)
                return new ValidateVoucherResult(false,
                    "Bạn cần đăng nhập để sử dụng mã giảm giá này", null, null, 0);

            var customerUsageCount = await db.VoucherUsages
                .CountAsync(u => u.VoucherId == voucher.Id
                    && u.CustomerId == query.CustomerId
                    && u.Status != VoucherUsageStatus.Released, ct);
            
            if (customerUsageCount >= voucher.MaxUsagePerCustomer)
                return new ValidateVoucherResult(false, 
                    "Bạn đã sử dụng hết lượt cho mã giảm giá này", null, null, 0);
        }

        if (voucher.FirstPurchaseOnly)
        {
            if (!query.CustomerId.HasValue || !query.CustomerTotalOrders.HasValue || query.CustomerTotalOrders > 0)
                return new ValidateVoucherResult(false,
                    "Mã giảm giá này chỉ áp dụng cho đơn hàng đầu tiên", null, null, 0);
        }

        var discountBase = ResolveDiscountBase(voucher, query);
        if (discountBase <= 0)
        {
            return new ValidateVoucherResult(false,
                "Mã giảm giá không áp dụng cho sản phẩm trong giỏ hàng", null, null, 0);
        }

        decimal discountAmount = voucher.Type switch
        {
            VoucherType.Percentage => discountBase * (voucher.DiscountValue / 100),
            VoucherType.FixedAmount => voucher.DiscountValue,
            VoucherType.FreeShipping => 0,
            _ => 0
        };

        if (voucher.MaximumDiscountAmount.HasValue && discountAmount > voucher.MaximumDiscountAmount)
            discountAmount = voucher.MaximumDiscountAmount.Value;

        if (voucher.Type != VoucherType.FreeShipping && discountAmount > discountBase)
            discountAmount = discountBase;

        return new ValidateVoucherResult(true, null, voucher.Id, voucher.Type, discountAmount);
    }

    private static decimal ResolveDiscountBase(Voucher voucher, ValidateVoucherQuery query)
    {
        var hasLineScope =
            voucher.ApplicableCategoryId.HasValue ||
            voucher.ApplicableProductIds?.Count > 0 ||
            !voucher.CanCombineWithSalePrice;

        if (!hasLineScope)
            return query.CartQuoteAmount;

        if (query.Lines is { Count: > 0 })
        {
            var eligibleLines = query.Lines.AsEnumerable();

            if (voucher.ApplicableCategoryId.HasValue)
                eligibleLines = eligibleLines.Where(l => l.CategoryId == voucher.ApplicableCategoryId.Value);

            if (voucher.ApplicableProductIds?.Count > 0)
                eligibleLines = eligibleLines.Where(l => voucher.ApplicableProductIds.Contains(l.ProductId));

            if (!voucher.CanCombineWithSalePrice)
                eligibleLines = eligibleLines.Where(l => !l.IsOnSale);

            return eligibleLines.Sum(l => l.LineTotal);
        }

        if (voucher.ApplicableCategoryId.HasValue)
        {
            if (!query.CategoryId.HasValue || query.CategoryId != voucher.ApplicableCategoryId)
                return 0;
        }

        if (voucher.ApplicableProductIds?.Count > 0)
        {
            if (query.ProductIds is null || !query.ProductIds.Any(pid => voucher.ApplicableProductIds.Contains(pid)))
                return 0;
        }

        return query.CartQuoteAmount;
    }
}
