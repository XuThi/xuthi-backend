using Messaging.Events;
using ProductCatalog.Infrastructure.Dtos;

namespace ProductCatalog.Events;

public class ProductUpdatedEvent : IntegrationEvent
{
    public Guid ProductId { get; set; }
    public ProductInfo Product { get; set; } = default!;
}
