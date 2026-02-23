using Core.DDD;

namespace ProductCatalog.Products.Events;

public record VariantCreatedEvent(Guid VariantId, Guid ProductId) : IDomainEvent;
