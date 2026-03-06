using ProductCatalog.Products.Models;

namespace ProductCatalog.Products.Services;

public interface IStockReservationService
{
    /// <summary>
    /// Reserve stock for a list of variant-quantity pairs. Returns reservation IDs.
    /// Deducts StockQuantity immediately; on expiry, the cleanup job releases them.
    /// </summary>
    Task<List<Guid>> ReserveStockAsync(
        string sessionKey,
        List<(Guid VariantId, int Quantity)> items,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Confirm reservations when an order is placed. Marks them as Confirmed so
    /// the cleanup job won't release them.
    /// </summary>
    Task ConfirmReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Release all reservations for a session (e.g. user abandoned checkout).
    /// Adds stock back to variants.
    /// </summary>
    Task ReleaseReservationsAsync(string sessionKey, CancellationToken ct = default);

    /// <summary>
    /// Clean up all expired reservations. Called by the background service.
    /// </summary>
    Task<int> CleanupExpiredReservationsAsync(CancellationToken ct = default);
}
