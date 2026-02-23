using Core.DDD;

namespace ProductCatalog.Products.Events;

public record ProductDeletedEvent(Guid ProductId) : IDomainEvent;
