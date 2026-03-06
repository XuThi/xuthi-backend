using Core.DDD;

namespace ProductCatalog.Products.Events;

public record ProductCreatedEvent(
    Guid ProductId,
    string ProductName,
    string? ImageUrl,
    string? Slug,
    decimal? BasePrice) : IDomainEvent;
