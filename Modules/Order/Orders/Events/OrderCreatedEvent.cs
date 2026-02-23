using Core.DDD;

namespace Order.Orders.Events;

public record OrderCreatedEvent(Guid OrderId, string OrderNumber, decimal Total) : IDomainEvent;
