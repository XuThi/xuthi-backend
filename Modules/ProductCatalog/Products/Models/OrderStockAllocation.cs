using Core.DDD;

namespace ProductCatalog.Products.Models;

public class OrderStockAllocation : Entity<Guid>
{
    public Guid? OrderId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public OrderStockAllocationState State { get; set; } = OrderStockAllocationState.Held;
    public DateTime? HeldAt { get; set; }
    public DateTime? CommittedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? RestoredAt { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    public string? LegacySessionKey { get; set; }
}

public enum OrderStockAllocationState
{
    Held,
    Committed,
    Released,
    Restored
}
