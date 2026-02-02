using Promotion.Infrastructure.Entity;

namespace Promotion.Features.Vouchers;

// DTOs
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

// Commands & Queries
public record GetVouchersQuery(bool? IsActive = null, bool? ValidOnly = null) 
    : IQuery<GetVouchersResult>;
public record GetVouchersResult(List<VoucherDto> Vouchers);

public record GetVoucherQuery(Guid Id) : IQuery<GetVoucherResult>;
public record GetVoucherResult(VoucherDto? Voucher);

public record CreateVoucherCommand(CreateVoucherRequest Request) : ICommand<CreateVoucherResult>;
public record CreateVoucherResult(Guid Id);

public record UpdateVoucherCommand(Guid Id, UpdateVoucherRequest Request) : ICommand<UpdateVoucherResult>;
public record UpdateVoucherResult(bool Success);

public record DeleteVoucherCommand(Guid Id) : ICommand<DeleteVoucherResult>;
public record DeleteVoucherResult(bool Success);

// Validation request - for cart/checkout
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
