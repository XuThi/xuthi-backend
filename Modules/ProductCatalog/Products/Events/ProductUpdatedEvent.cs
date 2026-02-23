using Core.DDD;

namespace ProductCatalog.Products.Events;

public record ProductUpdatedEvent(Guid ProductId) : IDomainEvent;
