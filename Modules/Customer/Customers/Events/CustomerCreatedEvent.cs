using Core.DDD;

namespace Customer.Customers.Events;

public record CustomerCreatedEvent(Guid CustomerId, string? Name) : IDomainEvent;
