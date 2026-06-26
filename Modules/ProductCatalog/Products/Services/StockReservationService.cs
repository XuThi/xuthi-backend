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
    private const string OrderSessionPrefix = "order:";

    public async Task<List<Guid>> ReserveStockAsync(
        string sessionKey,
        List<(Guid VariantId, int Quantity)> items,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.Add(ttl ?? DefaultTtl);
        var orderId = TryGetOrderId(sessionKey);
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
            if (quantity <= 0)
                throw new InvalidOperationException("Stock allocation quantity must be greater than zero.");

            if (!variantSkuMap.TryGetValue(variantId, out var sku))
                throw new InvalidOperationException($"Variant {variantId} not found");

            if (orderId.HasValue)
            {
                var existingForOrder = await db.OrderStockAllocations
                    .SingleOrDefaultAsync(r => r.OrderId == orderId
                        && r.ProductVariantId == variantId, ct);

                if (existingForOrder is not null)
                {
                    if (existingForOrder.State != OrderStockAllocationState.Held
                        || existingForOrder.Quantity != quantity)
                    {
                        throw new InvalidOperationException(
                            $"Conflicting stock allocation for order {orderId} and variant {variantId}.");
                    }

                    reservationIds.Add(existingForOrder.Id);
                    continue;
                }
            }

            // Release any existing legacy non-order hold for this session + variant before creating a fresh hold.
            var existingForSession = await db.OrderStockAllocations
                .Where(r => !r.OrderId.HasValue
                    && r.ProductVariantId == variantId
                    && r.LegacySessionKey == sessionKey
                    && r.State == OrderStockAllocationState.Held)
                .ToListAsync(ct);

            foreach (var existing in existingForSession)
            {
                existing.State = OrderStockAllocationState.Released;
                existing.ReleasedAt ??= now;
                await IncrementStockAsync(existing.ProductVariantId, existing.Quantity, ct);
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

            var allocation = new OrderStockAllocation
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductVariantId = variantId,
                Quantity = quantity,
                LegacySessionKey = sessionKey,
                State = OrderStockAllocationState.Held,
                HeldAt = now,
                HoldExpiresAt = expiresAt
            };

            db.OrderStockAllocations.Add(allocation);
            reservationIds.Add(allocation.Id);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Held stock for session {SessionKey}: {Count} items, expires at {ExpiresAt}",
            sessionKey, requestedItems.Count, expiresAt);

        // Tell the cleanup background service to start checking again.
        BackgroundServices.StockReservationCleanupService.RequireCheck = true;

        return reservationIds;
    }

    public async Task ConfirmReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default)
    {
        var sessionOrderId = TryGetOrderId(sessionKey);
        var allocations = await db.OrderStockAllocations
            .Where(r => r.State == OrderStockAllocationState.Held
                && (r.LegacySessionKey == sessionKey
                    || (sessionOrderId.HasValue && r.OrderId == sessionOrderId)))
            .ToListAsync(ct);

        if (allocations.Count == 0)
        {
            logger.LogWarning("No held stock allocations found for session {SessionKey}", sessionKey);
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var allocation in allocations)
        {
            allocation.State = OrderStockAllocationState.Committed;
            allocation.OrderId = orderId;
            allocation.CommittedAt ??= now;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Committed {Count} stock allocations for session {SessionKey}, order {OrderId}",
            allocations.Count, sessionKey, orderId);
    }

    public async Task ReleaseReservationsAsync(string sessionKey, CancellationToken ct = default)
    {
        var sessionOrderId = TryGetOrderId(sessionKey);
        var allocations = await db.OrderStockAllocations
            .Where(r => r.State == OrderStockAllocationState.Held
                && (r.LegacySessionKey == sessionKey
                    || (sessionOrderId.HasValue && r.OrderId == sessionOrderId)))
            .ToListAsync(ct);

        if (allocations.Count == 0)
            return;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        var now = DateTime.UtcNow;
        foreach (var allocation in allocations)
        {
            allocation.State = OrderStockAllocationState.Released;
            allocation.ReleasedAt ??= now;
            await IncrementStockAsync(allocation.ProductVariantId, allocation.Quantity, ct);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Released {Count} held stock allocations for session {SessionKey}",
            allocations.Count, sessionKey);
    }

    public async Task RestoreConfirmedReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default)
    {
        var allocations = await db.OrderStockAllocations
            .Where(r => (r.LegacySessionKey == sessionKey || r.OrderId == orderId)
                && r.OrderId == orderId
                && r.State == OrderStockAllocationState.Committed)
            .ToListAsync(ct);

        if (allocations.Count == 0)
            return;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        var now = DateTime.UtcNow;
        foreach (var allocation in allocations)
        {
            allocation.State = OrderStockAllocationState.Restored;
            allocation.RestoredAt ??= now;
            await IncrementStockAsync(allocation.ProductVariantId, allocation.Quantity, ct);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Restored stock for {Count} committed allocations from order {OrderId}",
            allocations.Count, orderId);
    }

    public async Task<int> CleanupExpiredReservationsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var expiredAllocations = await db.OrderStockAllocations
            .Where(r => r.State == OrderStockAllocationState.Held
                && r.HoldExpiresAt.HasValue
                && r.HoldExpiresAt <= now)
            .ToListAsync(ct);

        if (expiredAllocations.Count == 0)
            return 0;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        foreach (var allocation in expiredAllocations)
        {
            allocation.State = OrderStockAllocationState.Released;
            allocation.ReleasedAt ??= now;
            await IncrementStockAsync(allocation.ProductVariantId, allocation.Quantity, ct);
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Cleaned up {Count} expired stock allocations", expiredAllocations.Count);

        return expiredAllocations.Count;
    }

    private Task<int> IncrementStockAsync(Guid variantId, int quantity, CancellationToken ct)
    {
        return db.Variants
            .Where(v => v.Id == variantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(v => v.StockQuantity, v => v.StockQuantity + quantity), ct);
    }

    private static Guid? TryGetOrderId(string sessionKey)
    {
        if (!sessionKey.StartsWith(OrderSessionPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return Guid.TryParse(sessionKey[OrderSessionPrefix.Length..], out var orderId)
            ? orderId
            : null;
    }
}
