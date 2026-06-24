using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductCatalog.Data;
using ProductCatalog.Products.Models;

namespace ProductCatalog.Products.Services;

public class StockReservationService(
    ProductCatalogDbContext db,
    ILogger<StockReservationService> logger) : IStockReservationService
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public async Task<List<Guid>> ReserveStockAsync(
        string sessionKey,
        List<(Guid VariantId, int Quantity)> items,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var expiresAt = DateTime.UtcNow.Add(ttl ?? DefaultTtl);
        var requestedItems = items
            .GroupBy(i => i.VariantId)
            .Select(g => (VariantId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var variantIds = requestedItems.Select(i => i.VariantId).ToList();
        var variantSkuMap = await db.Variants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Sku, ct);

        var reservationIds = new List<Guid>();
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        foreach (var (variantId, quantity) in requestedItems)
        {
            if (!variantSkuMap.TryGetValue(variantId, out var sku))
                throw new InvalidOperationException($"Variant {variantId} not found");

            // Release any existing reservation for this session + variant before creating a fresh hold.
            var existingForSession = await db.StockReservations
                .Where(r => r.VariantId == variantId
                    && r.SessionKey == sessionKey
                    && r.Status == StockReservationStatus.Reserved)
                .ToListAsync(ct);

            foreach (var existing in existingForSession)
            {
                existing.Status = StockReservationStatus.Released;
                await IncrementStockAsync(existing.VariantId, existing.Quantity, ct);
            }

            // Atomic stock hold. Under concurrent checkout, only transactions that
            // can satisfy StockQuantity >= quantity will update a row.
            var rowsAffected = await db.Variants
                .Where(v => v.Id == variantId
                    && !v.IsDeleted
                    && v.StockQuantity >= quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.StockQuantity, v => v.StockQuantity - quantity), ct);

            if (rowsAffected == 0)
            {
                var remainingStock = await db.Variants
                    .Where(v => v.Id == variantId)
                    .Select(v => (int?)v.StockQuantity)
                    .FirstOrDefaultAsync(ct) ?? 0;

                throw new InvalidOperationException(
                    $"Khong du ton kho cho SKU {sku}. Chi con {remainingStock} san pham co san.");
            }

            var reservation = new StockReservation
            {
                Id = Guid.NewGuid(),
                VariantId = variantId,
                Quantity = quantity,
                SessionKey = sessionKey,
                Status = StockReservationStatus.Reserved,
                ExpiresAt = expiresAt
            };

            db.StockReservations.Add(reservation);
            reservationIds.Add(reservation.Id);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Reserved stock for session {SessionKey}: {Count} items, expires at {ExpiresAt}",
            sessionKey, requestedItems.Count, expiresAt);

        // Tell the cleanup background service to start checking again.
        BackgroundServices.StockReservationCleanupService.RequireCheck = true;

        return reservationIds;
    }

    public async Task ConfirmReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default)
    {
        var reservations = await db.StockReservations
            .Where(r => r.SessionKey == sessionKey && r.Status == StockReservationStatus.Reserved)
            .ToListAsync(ct);

        if (reservations.Count == 0)
        {
            logger.LogWarning("No active reservations found for session {SessionKey}", sessionKey);
            return;
        }

        foreach (var reservation in reservations)
        {
            reservation.Status = StockReservationStatus.Confirmed;
            reservation.OrderId = orderId;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Confirmed {Count} reservations for session {SessionKey}, order {OrderId}",
            reservations.Count, sessionKey, orderId);
    }

    public async Task ReleaseReservationsAsync(string sessionKey, CancellationToken ct = default)
    {
        var reservations = await db.StockReservations
            .Where(r => r.SessionKey == sessionKey && r.Status == StockReservationStatus.Reserved)
            .ToListAsync(ct);

        if (reservations.Count == 0)
            return;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        foreach (var reservation in reservations)
        {
            reservation.Status = StockReservationStatus.Released;
            await IncrementStockAsync(reservation.VariantId, reservation.Quantity, ct);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Released {Count} reservations for session {SessionKey}",
            reservations.Count, sessionKey);
    }

    public async Task RestoreConfirmedReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default)
    {
        var reservations = await db.StockReservations
            .Where(r => r.SessionKey == sessionKey
                && r.OrderId == orderId
                && r.Status == StockReservationStatus.Confirmed)
            .ToListAsync(ct);

        if (reservations.Count == 0)
            return;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        foreach (var reservation in reservations)
        {
            reservation.Status = StockReservationStatus.Released;
            await IncrementStockAsync(reservation.VariantId, reservation.Quantity, ct);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Restored stock for {Count} confirmed reservations from order {OrderId}",
            reservations.Count, orderId);
    }

    public async Task<int> CleanupExpiredReservationsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var expiredReservations = await db.StockReservations
            .Where(r => r.Status == StockReservationStatus.Reserved && r.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expiredReservations.Count == 0)
            return 0;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        foreach (var reservation in expiredReservations)
        {
            reservation.Status = StockReservationStatus.Released;
            await IncrementStockAsync(reservation.VariantId, reservation.Quantity, ct);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Cleaned up {Count} expired stock reservations", expiredReservations.Count);

        return expiredReservations.Count;
    }

    private Task<int> IncrementStockAsync(Guid variantId, int quantity, CancellationToken ct)
    {
        return db.Variants
            .Where(v => v.Id == variantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.StockQuantity, v => v.StockQuantity + quantity), ct);
    }
}
