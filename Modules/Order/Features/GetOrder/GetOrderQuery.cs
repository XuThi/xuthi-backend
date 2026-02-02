namespace Order.Features.GetOrder;

public record GetOrderQuery(Guid? Id = null, string? OrderNumber = null) : IQuery<OrderDetailResult>;

public record OrderDetailResult(
    Guid Id,
    string OrderNumber,
    
    // Customer
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    
    // Shipping
    string ShippingAddress,
    string ShippingCity,
    string ShippingDistrict,
    string ShippingWard,
    string? ShippingNote,
    
    // Totals
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal Total,
    
    // Voucher
    string? VoucherCode,
    
    // Status
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    
    // Timestamps
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    
    // Items
    List<OrderItemDetail> Items
);

public record OrderItemDetail(
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
    decimal TotalPrice
);
