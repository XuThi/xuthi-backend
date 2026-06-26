using Core.DDD;

namespace ProductCatalog.Products.Models;

public class OrderStockLifecycleEventFact : Entity<Guid>
{
    public Guid OrderId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string LinesJson { get; set; } = "[]";
}
