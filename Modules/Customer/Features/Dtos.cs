using Customer.Infrastructure.Entity;

namespace Customer.Features.Customers;

// Shared DTOs for Customer module

public record CustomerDto(
    Guid Id,
    string ExternalUserId,
    string Email,
    string? FullName,
    string? Phone,
    CustomerTier Tier,
    int LoyaltyPoints,
    decimal TotalSpent,
    int TotalOrders,
    decimal TierDiscountPercentage,
    DateTime CreatedAt,
    DateTime? LastOrderAt);

public record CustomerDetailDto(
    Guid Id,
    string ExternalUserId,
    string Email,
    string? FullName,
    string? Phone,
    DateTime? DateOfBirth,
    Gender? Gender,
    CustomerTier Tier,
    int LoyaltyPoints,
    decimal TotalSpent,
    int TotalOrders,
    decimal TierDiscountPercentage,
    bool AcceptsMarketing,
    bool AcceptsSms,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    DateTime? LastOrderAt,
    List<CustomerAddressDto> Addresses);

public record CustomerAddressDto(
    Guid Id,
    string Label,
    string RecipientName,
    string Phone,
    string Address,
    string Ward,
    string District,
    string City,
    string? Note,
    bool IsDefault);
