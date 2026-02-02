using Customer.Infrastructure.Entity;

namespace Customer.Features.Customers;

// TODO: Seperate all of this

// ============ DTOs ============
public record CustomerDto(
    Guid Id,
    string KeycloakUserId,
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
    string KeycloakUserId,
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

// ============ Commands & Queries ============

// Get/Create customer profile (called on first login via Keycloak)
public record GetOrCreateCustomerQuery(string KeycloakUserId, string Email, string? FullName = null)
    : IQuery<GetOrCreateCustomerResult>;
public record GetOrCreateCustomerResult(CustomerDto Customer, bool IsNew);

// Get customer by ID
public record GetCustomerQuery(Guid Id) : IQuery<GetCustomerResult>;
public record GetCustomerResult(CustomerDetailDto? Customer);

// Get customer by Keycloak ID
public record GetCustomerByKeycloakIdQuery(string KeycloakUserId) : IQuery<GetCustomerByKeycloakIdResult>;
public record GetCustomerByKeycloakIdResult(CustomerDetailDto? Customer);

// Update customer profile
public record UpdateCustomerCommand(
    Guid Id,
    string? FullName,
    string? Phone,
    DateTime? DateOfBirth,
    Gender? Gender,
    bool? AcceptsMarketing,
    bool? AcceptsSms) : ICommand<UpdateCustomerResult>;
public record UpdateCustomerResult(bool Success);

// Add customer order stats (called from Order module)
public record AddCustomerOrderCommand(
    Guid CustomerId,
    decimal OrderTotal,
    int PointsEarned,
    Guid OrderId) : ICommand<AddCustomerOrderResult>;
public record AddCustomerOrderResult(CustomerTier NewTier, int TotalPoints);

// === Address Management ===

public record AddAddressCommand(
    Guid CustomerId,
    string Label,
    string RecipientName,
    string Phone,
    string Address,
    string Ward,
    string District,
    string City,
    string? Note,
    bool SetAsDefault = false) : ICommand<AddAddressResult>;
public record AddAddressResult(Guid AddressId);

public record UpdateAddressCommand(
    Guid AddressId,
    string Label,
    string RecipientName,
    string Phone,
    string Address,
    string Ward,
    string District,
    string City,
    string? Note,
    bool IsDefault) : ICommand<UpdateAddressResult>;
public record UpdateAddressResult(bool Success);

public record DeleteAddressCommand(Guid AddressId) : ICommand<DeleteAddressResult>;
public record DeleteAddressResult(bool Success);

public record SetDefaultAddressCommand(Guid CustomerId, Guid AddressId) : ICommand<SetDefaultAddressResult>;
public record SetDefaultAddressResult(bool Success);
