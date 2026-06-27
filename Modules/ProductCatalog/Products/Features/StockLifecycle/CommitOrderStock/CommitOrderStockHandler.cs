using Contracts;
using System.Text.Json;

namespace ProductCatalog.Products.Features.StockLifecycle.CommitOrderStock;

internal class CommitOrderStockHandler(ProductCatalogDbContext db)
    : ICommandHandler<CommitOrderStockCommand, StockLifecycleResult>
{
    public async Task<StockLifecycleResult> Handle(
        CommitOrderStockCommand command,
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

        if (command.ExpectedPriorState != StockLifecycleExpectedPriorState.Held)
        {
            return StockLifecycleResult.ValidationFailed(
                [new StockLifecycleValidationDetail(
                    null,
                    "UnsupportedExpectedPriorState",
                    "Only Held prior stock state is supported by this commit path.")]);
        }

        var allocations = await LoadAllocationsAsync(command.OrderId, cancellationToken);
        var commitDecision = EvaluateExistingCommit(command.OrderId, normalizedLines, allocations);

        if (!commitDecision.ShouldCommit)
            return commitDecision.Result;

        var now = DateTime.UtcNow;
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var rowsAffected = await db.OrderStockAllocations
                .Where(row => row.OrderId == command.OrderId
                    && row.State == OrderStockAllocationState.Held)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.State, OrderStockAllocationState.Committed)
                    .SetProperty(row => row.CommittedAt, now),
                    cancellationToken);

            if (rowsAffected != allocations.Count)
            {
                if (transaction is not null)
                    await transaction.RollbackAsync(cancellationToken);

                db.ChangeTracker.Clear();
                var afterRace = EvaluateExistingCommit(
                    command.OrderId,
                    normalizedLines,
                    await LoadAllocationsAsync(command.OrderId, cancellationToken));

                if (!afterRace.ShouldCommit)
                    return afterRace.Result;

                throw new DbUpdateConcurrencyException(
                    "Stock lifecycle commit did not apply to all held allocations.");
            }

            db.OrderStockLifecycleEventFacts.Add(new OrderStockLifecycleEventFact
            {
                Id = Guid.NewGuid(),
                OrderId = command.OrderId,
                EventType = nameof(OrderStockCommitted),
                IdempotencyKey = $"stock:{command.OrderId}:committed",
                OccurredAt = now,
                LinesJson = JsonSerializer.Serialize(commitDecision.Result.Lines)
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
            var afterRace = EvaluateExistingCommit(
                command.OrderId,
                normalizedLines,
                await LoadAllocationsAsync(command.OrderId, cancellationToken));

            if (!afterRace.ShouldCommit)
                return afterRace.Result;

            throw;
        }

        return commitDecision.Result;
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

    private static CommitDecision EvaluateExistingCommit(
        Guid orderId,
        IReadOnlyList<StockLifecycleLine> normalizedLines,
        IReadOnlyList<OrderStockAllocation> allocations)
    {
        var existingLines = allocations
            .Select(allocation => new StockLifecycleLine(
                allocation.ProductVariantId,
                allocation.Quantity))
            .OrderBy(line => line.ProductVariantId)
            .ToList();

        if (allocations.Count == 0)
            return CommitDecision.Done(Conflict(
                normalizedLines,
                [],
                OrderStockAllocationState.Held.ToString(),
                "Missing",
                "No stock lifecycle allocation exists for this Order id.",
                null));

        var linesMatch = normalizedLines.SequenceEqual(existingLines);

        if (linesMatch && allocations.All(allocation => allocation.State == OrderStockAllocationState.Committed))
            return CommitDecision.Done(StockLifecycleResult.Succeeded(existingLines));

        if (linesMatch && allocations.All(allocation => allocation.State == OrderStockAllocationState.Held))
            return CommitDecision.Commit(StockLifecycleResult.Succeeded(existingLines));

        var existingState = string.Join(
            ",",
            allocations
                .Select(allocation => allocation.State.ToString())
                .Distinct()
                .Order());

        return CommitDecision.Done(Conflict(
            normalizedLines,
            existingLines,
            OrderStockAllocationState.Held.ToString(),
            existingState,
            $"Existing stock lifecycle allocation for Order {orderId} cannot be committed as the requested Stock Hold.",
            allocations
                .Where(allocation => allocation.HoldExpiresAt.HasValue)
                .Select(allocation => allocation.HoldExpiresAt!.Value)
                .DefaultIfEmpty()
                .Min()));
    }

    private static StockLifecycleResult Conflict(
        IReadOnlyList<StockLifecycleLine> expectedLines,
        IReadOnlyList<StockLifecycleLine> existingLines,
        string expectedState,
        string existingState,
        string reason,
        DateTime? existingHoldExpiresAt)
    {
        return StockLifecycleResult.Conflicted(
            expectedLines,
            new StockLifecycleConflictDetail(
                reason,
                expectedState,
                existingState,
                expectedLines,
                existingLines,
                null,
                existingHoldExpiresAt == default ? null : existingHoldExpiresAt));
    }

    private readonly record struct CommitDecision(
        bool ShouldCommit,
        StockLifecycleResult Result)
    {
        public static CommitDecision Commit(StockLifecycleResult result)
            => new(true, result);

        public static CommitDecision Done(StockLifecycleResult result)
            => new(false, result);
    }
}
