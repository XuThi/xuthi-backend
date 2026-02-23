using Core.DDD;
using ProductCatalog.Products.Dtos;

namespace ProductCatalog.Products.Events;

public record ProductCreatedEvent(Guid ProductId) : IDomainEvent;
