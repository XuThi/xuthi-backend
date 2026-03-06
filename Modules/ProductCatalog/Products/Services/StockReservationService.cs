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
        var variantIds = items.Select(i => i.VariantId).ToList();

        var variants = await db.Variants
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(ct);

        var reservationIds = new List<Guid>();

        foreach (var (variantId, quantity) in items)
        {
            var variant = variants.FirstOrDefault(v => v.Id == variantId)
                ?? throw new InvalidOperationException($"Variant {variantId} not found");

            // Check available stock (actual stock minus active reservations that are NOT for this session)
            var existingReserved = await db.StockReservations
                .Where(r => r.VariantId == variantId
                    && r.Status == StockReservationStatus.Reserved
                    && r.ExpiresAt > DateTime.UtcNow
                    && r.SessionKey != sessionKey)
                .SumAsync(r => r.Quantity, ct);

            var availableStock = variant.StockQuantity - existingReserved;

            if (availableStock < quantity)
                throw new InvalidOperationException(
                    $"Không đủ tồn kho cho SKU {variant.Sku}. Chỉ còn {availableStock} sản phẩm có sẵn.");

            // Release any existing reservation for this session + variant
            var existingForSession = await db.StockReservations
                .Where(r => r.VariantId == variantId
                    && r.SessionKey == sessionKey
                    && r.Status == StockReservationStatus.Reserved)
                .ToListAsync(ct);

            foreach (var existing in existingForSession)
            {
                existing.Status = StockReservationStatus.Released;
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

        logger.LogInformation(
            "Reserved stock for session {SessionKey}: {Count} items, expires at {ExpiresAt}",
            sessionKey, items.Count, expiresAt);

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

        // Deduct actual stock and confirm reservations
        var variantIds = reservations.Select(r => r.VariantId).Distinct().ToList();
        var variants = await db.Variants
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            var variant = variants.FirstOrDefault(v => v.Id == reservation.VariantId);
            if (variant is not null)
            {
                variant.StockQuantity -= reservation.Quantity;
                if (variant.StockQuantity < 0) variant.StockQuantity = 0;
            }

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

        foreach (var reservation in reservations)
        {
            reservation.Status = StockReservationStatus.Released;
        }

        if (reservations.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Released {Count} reservations for session {SessionKey}",
                reservations.Count, sessionKey);
        }
    }

    public async Task<int> CleanupExpiredReservationsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var expiredReservations = await db.StockReservations
            .Where(r => r.Status == StockReservationStatus.Reserved && r.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expiredReservations.Count == 0) return 0;

        foreach (var reservation in expiredReservations)
        {
            reservation.Status = StockReservationStatus.Released;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Cleaned up {Count} expired stock reservations", expiredReservations.Count);

        return expiredReservations.Count;
    }
}
