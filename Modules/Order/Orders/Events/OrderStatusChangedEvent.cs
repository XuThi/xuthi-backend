using Core.DDD;

namespace Order.Orders.Events;

public record OrderStatusChangedEvent(Guid OrderId, string PreviousStatus, string NewStatus) : IDomainEvent;
