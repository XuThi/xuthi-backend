namespace Promotion.Vouchers.Dtos;

public record VoucherDto(
    Guid Id,
    string Code,
    string? Description,
    VoucherType Type,
    decimal DiscountValue,
    decimal? MinimumOrderAmount,
    decimal? MaximumDiscountAmount,
    int? MaxUsageCount,
    int CurrentUsageCount,
    int? MaxUsagePerCustomer,
    DateTime StartDate,
    DateTime EndDate,
    Guid? ApplicableCategoryId,
    List<Guid>? ApplicableProductIds,
    int? MinimumCustomerTier,
    bool CanCombineWithOtherVouchers,
    bool CanCombineWithSalePrice,
    bool FirstPurchaseOnly,
    bool IsActive,
    bool IsValid);
