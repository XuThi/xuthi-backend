using Messaging.Events;

namespace ProductCatalog.Events;

public class ProductDeletedEvent : IntegrationEvent
{
    public Guid ProductId { get; set; }
}
