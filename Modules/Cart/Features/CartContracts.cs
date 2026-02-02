using Cart.Infrastructure.Entity;

namespace Cart.Features;

// TODO: Seperate all of this in different files

// ============ DTOs ============
public record CartDto(
    Guid Id,
    string? SessionId,
    Guid? CustomerId,
    List<CartItemDto> Items,
    decimal Subtotal,
    decimal VoucherDiscount,
    string? AppliedVoucherCode,
    decimal Total,
    int TotalItems);

public record CartItemDto(
    Guid Id,
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantSku,
    string? VariantDescription,
    string? ImageUrl,
    decimal UnitPrice,
    decimal? CompareAtPrice,
    int Quantity,
    decimal TotalPrice,
    int AvailableStock,
    bool IsInStock,
    bool IsOnSale);

// ============ Commands & Queries ============

// Get or create cart by session/customer
public record GetCartQuery(string? SessionId, Guid? CustomerId) : IQuery<GetCartResult>;
public record GetCartResult(CartDto? Cart);

// Add item to cart
public record AddToCartCommand(
    string? SessionId,
    Guid? CustomerId,
    Guid ProductId,
    Guid VariantId,
    int Quantity = 1) : ICommand<AddToCartResult>;
public record AddToCartResult(Guid CartId, CartDto Cart);

// Update item quantity
public record UpdateCartItemCommand(
    Guid CartId,
    Guid VariantId,
    int Quantity) : ICommand<UpdateCartItemResult>;
public record UpdateCartItemResult(bool Success, CartDto? Cart, string? ErrorMessage);

// Remove item from cart
public record RemoveFromCartCommand(
    Guid CartId,
    Guid VariantId) : ICommand<RemoveFromCartResult>;
public record RemoveFromCartResult(bool Success, CartDto? Cart);

// Clear cart
public record ClearCartCommand(Guid CartId) : ICommand<ClearCartResult>;
public record ClearCartResult(bool Success);

// Apply voucher to cart
public record ApplyVoucherCommand(
    Guid CartId,
    string VoucherCode) : ICommand<ApplyVoucherResult>;
public record ApplyVoucherResult(bool Success, string? ErrorMessage, decimal DiscountAmount, CartDto? Cart);

// Remove voucher from cart
public record RemoveVoucherCommand(Guid CartId) : ICommand<RemoveVoucherResult>;
public record RemoveVoucherResult(bool Success, CartDto? Cart);

// Sync cart prices with ProductCatalog (call before checkout)
public record SyncCartPricesCommand(Guid CartId) : ICommand<SyncCartPricesResult>;
public record SyncCartPricesResult(bool Success, CartDto? Cart, List<string>? Warnings);

// Merge anonymous cart to customer cart (after login)
public record MergeCartsCommand(
    string SessionId,
    Guid CustomerId) : ICommand<MergeCartsResult>;
public record MergeCartsResult(bool Success, CartDto? Cart);
