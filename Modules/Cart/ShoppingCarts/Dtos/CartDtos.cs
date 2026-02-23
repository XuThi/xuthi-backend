namespace Cart.ShoppingCarts.Dtos;

// Shared DTOs for Cart module - used by all feature handlers

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
