using Contracts;
using System.Text.Json;

namespace ProductCatalog.Products.Features.StockLifecycle.HoldOrderAttemptStock;

internal class HoldOrderAttemptStockHandler(ProductCatalogDbContext db)
    : ICommandHandler<HoldOrderAttemptStockCommand, StockLifecycleResult>
{
    public async Task<StockLifecycleResult> Handle(
        HoldOrderAttemptStockCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedLines = NormalizeLines(command.Lines);
        var quantityFailures = command.Lines
            .Where(line => line.Quantity <= 0)
            .Select(line => new StockLifecycleValidationDetail(
                line.ProductVariantId,
                "QuantityMustBePositive",
                "Stock lifecycle line quantity must be greater than zero."))
            .ToList();

        if (command.Lines.Count == 0)
        {
            quantityFailures.Add(new StockLifecycleValidationDetail(
                null,
                "LinesRequired",
                "At least one stock lifecycle line is required."));
        }

        if (quantityFailures.Count > 0)
            return StockLifecycleResult.ValidationFailed(quantityFailures);

        var existingAllocations = await db.OrderStockAllocations
            .Where(allocation => allocation.OrderId == command.OrderId)
            .ToListAsync(cancellationToken);

        if (existingAllocations.Count > 0)
            return EvaluateExistingHold(command, normalizedLines, existingAllocations);

        var variants = await db.Variants
            .Where(variant => normalizedLines.Select(line => line.ProductVariantId).Contains(variant.Id))
            .Select(variant => new
            {
                variant.Id,
                variant.StockQuantity,
                variant.IsActive,
                variant.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var variantsById = variants.ToDictionary(variant => variant.Id);
        var validationFailures = normalizedLines
            .Where(line => !variantsById.TryGetValue(line.ProductVariantId, out var variant)
                || variant.IsDeleted
                || !variant.IsActive)
            .Select(line => new StockLifecycleValidationDetail(
                line.ProductVariantId,
                "ProductVariantUnavailable",
                $"Product Variant {line.ProductVariantId} is missing, deleted, or inactive."))
            .ToList();

        if (validationFailures.Count > 0)
            return StockLifecycleResult.ValidationFailed(validationFailures);

        var insufficientStock = normalizedLines
            .Where(line => variantsById[line.ProductVariantId].StockQuantity < line.Quantity)
            .Select(line => new StockLifecycleInsufficientStockDetail(
                line.ProductVariantId,
                line.Quantity,
                variantsById[line.ProductVariantId].StockQuantity))
            .ToList();

        if (insufficientStock.Count > 0)
            return StockLifecycleResult.InsufficientStock(normalizedLines, insufficientStock);

        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        foreach (var line in normalizedLines)
        {
            var rowsAffected = await db.Variants
                .Where(variant => variant.Id == line.ProductVariantId
                    && !variant.IsDeleted
                    && variant.IsActive
                    && variant.StockQuantity >= line.Quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(variant => variant.StockQuantity, variant => variant.StockQuantity - line.Quantity),
                    cancellationToken);

            if (rowsAffected == 0)
            {
                var availableQuantity = await db.Variants
                    .Where(variant => variant.Id == line.ProductVariantId)
                    .Select(variant => (int?)variant.StockQuantity)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0;

                return StockLifecycleResult.InsufficientStock(
                    normalizedLines,
                    [new StockLifecycleInsufficientStockDetail(
                        line.ProductVariantId,
                        line.Quantity,
                        availableQuantity)]);
            }
        }

        var now = DateTime.UtcNow;
        db.OrderStockAllocations.AddRange(normalizedLines.Select(line =>
            new OrderStockAllocation
            {
                Id = Guid.NewGuid(),
                OrderId = command.OrderId,
                ProductVariantId = line.ProductVariantId,
                Quantity = line.Quantity,
                State = OrderStockAllocationState.Held,
                HeldAt = now,
                HoldExpiresAt = command.HoldExpiresAt
            }));
        db.OrderStockLifecycleEventFacts.Add(new OrderStockLifecycleEventFact
        {
            Id = Guid.NewGuid(),
            OrderId = command.OrderId,
            EventType = nameof(OrderStockHeld),
            IdempotencyKey = $"stock:{command.OrderId}:held",
            OccurredAt = now,
            LinesJson = JsonSerializer.Serialize(normalizedLines)
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);

            db.ChangeTracker.Clear();
            var allocationsAfterRace = await db.OrderStockAllocations
                .AsNoTracking()
                .Where(allocation => allocation.OrderId == command.OrderId)
                .ToListAsync(cancellationToken);

            if (allocationsAfterRace.Count > 0)
                return EvaluateExistingHold(command, normalizedLines, allocationsAfterRace);

            throw;
        }

        return StockLifecycleResult.Succeeded(normalizedLines);
    }

    private static List<StockLifecycleLine> NormalizeLines(IReadOnlyList<StockLifecycleLine> lines)
    {
        return lines
            .GroupBy(line => line.ProductVariantId)
            .Select(group => new StockLifecycleLine(
                group.Key,
                group.Sum(line => line.Quantity)))
            .OrderBy(line => line.ProductVariantId)
            .ToList();
    }

    private static StockLifecycleResult EvaluateExistingHold(
        HoldOrderAttemptStockCommand command,
        IReadOnlyList<StockLifecycleLine> normalizedLines,
        IReadOnlyList<OrderStockAllocation> existingAllocations)
    {
        var existingLines = existingAllocations
            .Select(allocation => new StockLifecycleLine(
                allocation.ProductVariantId,
                allocation.Quantity))
            .OrderBy(line => line.ProductVariantId)
            .ToList();

        var requestedLinesMatch = normalizedLines.SequenceEqual(existingLines);
        var existingHoldExpiresAt = existingAllocations
            .Where(allocation => allocation.HoldExpiresAt.HasValue)
            .Select(allocation => allocation.HoldExpiresAt!.Value)
            .DefaultIfEmpty()
            .Min();
        var allHeld = existingAllocations.All(allocation =>
            allocation.State == OrderStockAllocationState.Held);
        var compatibleExpiry = existingHoldExpiresAt != default
            && command.HoldExpiresAt <= existingHoldExpiresAt;

        if (requestedLinesMatch && allHeld && compatibleExpiry)
            return StockLifecycleResult.Succeeded(normalizedLines);

        var existingState = existingAllocations
            .Select(allocation => allocation.State.ToString())
            .Distinct()
            .Order()
            .Aggregate((left, right) => $"{left},{right}");

        return StockLifecycleResult.Conflicted(
            normalizedLines,
            new StockLifecycleConflictDetail(
                "Existing stock lifecycle allocation for this Order id is not compatible with the requested hold.",
                OrderStockAllocationState.Held.ToString(),
                existingState,
                normalizedLines,
                existingLines,
                command.HoldExpiresAt,
                existingHoldExpiresAt == default ? null : existingHoldExpiresAt));
    }
}
