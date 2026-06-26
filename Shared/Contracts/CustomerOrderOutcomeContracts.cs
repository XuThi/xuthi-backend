using Core.DDD;

namespace Contracts;

public enum CustomerOrderOutcome
{
    Delivered = 1,
    Returned = 2,
    Cancelled = 3
}

public record CustomerOrderOutcomeOccurred(
    Guid CustomerId,
    Guid OrderId,
    string OrderNumber,
    CustomerOrderOutcome Outcome,
    DateTime OccurredAt,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal Total) : IDomainEvent;
