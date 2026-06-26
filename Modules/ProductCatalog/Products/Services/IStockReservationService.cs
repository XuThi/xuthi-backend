using ProductCatalog.Products.Models;

namespace ProductCatalog.Products.Services;

public interface IStockReservationService
{
    /// <summary>
    /// Legacy-compatible stock hold facade. Stores held Order Stock Allocations and returns allocation IDs.
    /// Atomically deducts StockQuantity immediately; on expiry, the cleanup job releases the allocations.
    /// </summary>
    Task<List<Guid>> ReserveStockAsync(
        string sessionKey,
        List<(Guid VariantId, int Quantity)> items,
        TimeSpan? ttl = null,
        CancellationToken ct = default);

    /// <summary>
    /// Commit held Order Stock Allocations after payment/order commitment. Stock has already been held.
    /// </summary>
    Task ConfirmReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Release all held Order Stock Allocations for a session (e.g. user abandoned checkout).
    /// Adds stock back to variants.
    /// </summary>
    Task ReleaseReservationsAsync(string sessionKey, CancellationToken ct = default);

    /// <summary>
    /// Restore stock for committed Order Stock Allocations when a Created Order is cancelled.
    /// </summary>
    Task RestoreConfirmedReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Clean up all expired held Order Stock Allocations. Called by the background service.
    /// </summary>
    Task<int> CleanupExpiredReservationsAsync(CancellationToken ct = default);
}
