using Core.DDD;

namespace Order.Orders.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ShippingAddress,
    string ShippingCity,
    string ShippingDistrict,
    string ShippingWard,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal Total,
    List<OrderCreatedEventItem> Items
) : IDomainEvent;

public record OrderCreatedEventItem(
    string ProductName,
    string? VariantDescription,
    int Quantity,
    decimal TotalPrice
);
