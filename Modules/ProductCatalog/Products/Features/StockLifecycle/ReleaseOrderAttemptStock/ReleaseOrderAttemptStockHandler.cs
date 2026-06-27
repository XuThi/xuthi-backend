using Contracts;
using System.Text.Json;

namespace ProductCatalog.Products.Features.StockLifecycle.ReleaseOrderAttemptStock;

internal class ReleaseOrderAttemptStockHandler(ProductCatalogDbContext db)
    : ICommandHandler<ReleaseOrderAttemptStockCommand, StockLifecycleResult>
{
    public async Task<StockLifecycleResult> Handle(
        ReleaseOrderAttemptStockCommand command,
        CancellationToken cancellationToken)
    {
        var allocations = await LoadAllocationsAsync(command.OrderId, cancellationToken);
        var releaseDecision = EvaluateExistingRelease(command.OrderId, allocations);

        if (!releaseDecision.ShouldRelease)
            return releaseDecision.Result;

        var now = DateTime.UtcNow;
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            foreach (var allocation in allocations)
            {
                var rowsAffected = await db.OrderStockAllocations
                    .Where(row => row.Id == allocation.Id
                        && row.State == OrderStockAllocationState.Held)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.State, OrderStockAllocationState.Released)
                        .SetProperty(row => row.ReleasedAt, now),
                        cancellationToken);

                if (rowsAffected == 0)
                {
                    if (transaction is not null)
                        await transaction.RollbackAsync(cancellationToken);

                    db.ChangeTracker.Clear();
                    return EvaluateExistingRelease(
                        command.OrderId,
                        await LoadAllocationsAsync(command.OrderId, cancellationToken)).Result;
                }

                var variantRowsAffected = await db.Variants
                    .Where(variant => variant.Id == allocation.ProductVariantId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            variant => variant.StockQuantity,
                            variant => variant.StockQuantity + allocation.Quantity),
                        cancellationToken);

                if (variantRowsAffected == 0)
                    throw new DbUpdateConcurrencyException(
                        "Stock lifecycle release could not restore stock for an allocation.");
            }

            db.OrderStockLifecycleEventFacts.Add(new OrderStockLifecycleEventFact
            {
                Id = Guid.NewGuid(),
                OrderId = command.OrderId,
                EventType = nameof(OrderStockHoldReleased),
                IdempotencyKey = $"stock:{command.OrderId}:released",
                OccurredAt = now,
                LinesJson = JsonSerializer.Serialize(releaseDecision.Result.Lines)
            });

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);

            db.ChangeTracker.Clear();
            var afterRace = EvaluateExistingRelease(
                command.OrderId,
                await LoadAllocationsAsync(command.OrderId, cancellationToken));

            if (!afterRace.ShouldRelease)
                return afterRace.Result;

            throw;
        }

        return releaseDecision.Result;
    }

    private Task<List<OrderStockAllocation>> LoadAllocationsAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return db.OrderStockAllocations
            .AsNoTracking()
            .Where(allocation => allocation.OrderId == orderId)
            .OrderBy(allocation => allocation.ProductVariantId)
            .ToListAsync(cancellationToken);
    }

    private static ReleaseDecision EvaluateExistingRelease(
        Guid orderId,
        IReadOnlyList<OrderStockAllocation> allocations)
    {
        var existingLines = allocations
            .Select(allocation => new StockLifecycleLine(
                allocation.ProductVariantId,
                allocation.Quantity))
            .OrderBy(line => line.ProductVariantId)
            .ToList();

        if (allocations.Count == 0)
        {
            return ReleaseDecision.Done(StockLifecycleResult.Conflicted(
                [],
                new StockLifecycleConflictDetail(
                    "No stock lifecycle allocation exists for this Order id.",
                    OrderStockAllocationState.Held.ToString(),
                    "Missing",
                    [],
                    [],
                    null,
                    null)));
        }

        if (allocations.All(allocation => allocation.State == OrderStockAllocationState.Released))
            return ReleaseDecision.Done(StockLifecycleResult.Succeeded(existingLines));

        if (allocations.Any(allocation => allocation.State != OrderStockAllocationState.Held))
        {
            var existingState = allocations
                .Select(allocation => allocation.State.ToString())
                .Distinct()
                .Order()
                .Aggregate((left, right) => $"{left},{right}");

            return ReleaseDecision.Done(StockLifecycleResult.Conflicted(
                existingLines,
                new StockLifecycleConflictDetail(
                    $"Existing stock lifecycle allocation for Order {orderId} cannot be released as a Stock Hold.",
                    OrderStockAllocationState.Held.ToString(),
                    existingState,
                    existingLines,
                    existingLines,
                    null,
                    allocations
                        .Where(allocation => allocation.HoldExpiresAt.HasValue)
                        .Select(allocation => allocation.HoldExpiresAt!.Value)
                        .DefaultIfEmpty()
                        .Min())));
        }

        return ReleaseDecision.Release(StockLifecycleResult.Succeeded(existingLines));
    }

    private readonly record struct ReleaseDecision(
        bool ShouldRelease,
        StockLifecycleResult Result)
    {
        public static ReleaseDecision Release(StockLifecycleResult result)
            => new(true, result);

        public static ReleaseDecision Done(StockLifecycleResult result)
            => new(false, result);
    }
}
