using Core.DDD;

namespace ProductCatalog.Products.Models;

public class StockReservation : Entity<Guid>
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }

    /// <summary>
    /// SessionId or CartId that owns this reservation
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// If the reservation is confirmed (order placed), store the OrderId
    /// </summary>
    public Guid? OrderId { get; set; }

    public StockReservationStatus Status { get; set; } = StockReservationStatus.Reserved;

    public DateTime ExpiresAt { get; set; }
}

public enum StockReservationStatus
{
    Reserved,
    Confirmed,
    Released
}
