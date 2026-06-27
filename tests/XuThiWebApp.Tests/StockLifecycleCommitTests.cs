using Contracts;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Products.Models;

namespace XuThiWebApp.Tests;

public sealed class StockLifecycleCommitTests
{
    [Fact]
    public async Task Manual_payment_commit_creates_committed_allocation_and_decrements_stock()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.True(result.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(orderId, allocation.OrderId);
        Assert.Equal(item.VariantId, allocation.ProductVariantId);
        Assert.Equal(2, allocation.Quantity);
        Assert.Equal(OrderStockAllocationState.Committed, allocation.State);
        Assert.Null(allocation.HeldAt);
        Assert.NotNull(allocation.CommittedAt);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));

        var committed = Assert.Single(app.CommittedFacts);
        Assert.Equal(orderId, committed.OrderId);
        Assert.Equal($"stock:{orderId}:committed", committed.IdempotencyKey);
        var line = Assert.Single(committed.Lines);
        Assert.Equal(item.VariantId, line.ProductVariantId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task Manual_payment_commit_rejects_zero_and_negative_quantities()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            Guid.NewGuid(),
            StockLifecycleExpectedPriorState.None,
            [
                new StockLifecycleLine(item.VariantId, 0),
                new StockLifecycleLine(Guid.NewGuid(), -1)
            ]));

        Assert.Equal(StockLifecycleResultStatus.ValidationFailed, result.Status);
        Assert.Equal(2, result.ValidationDetails.Count);
        Assert.All(result.ValidationDetails, detail =>
            Assert.Equal("QuantityMustBePositive", detail.Code));
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task Manual_payment_commit_rejects_missing_deleted_and_inactive_variants()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var active = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var inactive = await app.SeedCatalogItemAsync(
            stockQuantity: 5,
            variant => variant.IsActive = false);
        var deleted = await app.SeedCatalogItemAsync(
            stockQuantity: 5,
            variant => variant.IsDeleted = true);
        var missingVariantId = Guid.NewGuid();

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            Guid.NewGuid(),
            StockLifecycleExpectedPriorState.None,
            [
                new StockLifecycleLine(active.VariantId, 1),
                new StockLifecycleLine(inactive.VariantId, 1),
                new StockLifecycleLine(deleted.VariantId, 1),
                new StockLifecycleLine(missingVariantId, 1)
            ]));

        Assert.Equal(StockLifecycleResultStatus.ValidationFailed, result.Status);
        Assert.Equal(3, result.ValidationDetails.Count);
        Assert.All(result.ValidationDetails, detail =>
            Assert.Equal("ProductVariantUnavailable", detail.Code));
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(5, await app.GetStockQuantityAsync(active.VariantId));
        Assert.Equal(5, await app.GetStockQuantityAsync(inactive.VariantId));
        Assert.Equal(5, await app.GetStockQuantityAsync(deleted.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task Manual_payment_commit_reports_insufficient_stock_without_changing_order_stock()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 1);

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            Guid.NewGuid(),
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.Equal(StockLifecycleResultStatus.InsufficientStock, result.Status);
        var detail = Assert.Single(result.InsufficientStockDetails);
        Assert.Equal(item.VariantId, detail.ProductVariantId);
        Assert.Equal(2, detail.RequestedQuantity);
        Assert.Equal(1, detail.AvailableQuantity);
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(1, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task Manual_payment_commit_is_whole_order_atomic_when_one_line_has_insufficient_stock()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var available = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var shortStock = await app.SeedCatalogItemAsync(stockQuantity: 1);

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            Guid.NewGuid(),
            StockLifecycleExpectedPriorState.None,
            [
                new StockLifecycleLine(available.VariantId, 2),
                new StockLifecycleLine(shortStock.VariantId, 2)
            ]));

        Assert.Equal(StockLifecycleResultStatus.InsufficientStock, result.Status);
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(5, await app.GetStockQuantityAsync(available.VariantId));
        Assert.Equal(1, await app.GetStockQuantityAsync(shortStock.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task Manual_payment_commit_retry_after_direct_commit_is_idempotent_without_duplicate_fact()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        var first = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [
                new StockLifecycleLine(item.VariantId, 1),
                new StockLifecycleLine(item.VariantId, 1)
            ]));
        var retry = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Committed, allocation.State);
        Assert.Equal(2, allocation.Quantity);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.CommittedFacts);
    }

    [Theory]
    [InlineData(OrderStockAllocationState.Held)]
    [InlineData(OrderStockAllocationState.Released)]
    [InlineData(OrderStockAllocationState.Restored)]
    public async Task Manual_payment_commit_conflicts_with_unexpected_existing_stock_lifecycle_state(
        OrderStockAllocationState state)
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        app.Db.OrderStockAllocations.Add(new OrderStockAllocation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductVariantId = item.VariantId,
            Quantity = 2,
            State = state,
            HeldAt = state == OrderStockAllocationState.Held ? now.AddMinutes(-5) : null,
            ReleasedAt = state == OrderStockAllocationState.Released ? now.AddMinutes(-4) : null,
            RestoredAt = state == OrderStockAllocationState.Restored ? now.AddMinutes(-3) : null,
            HoldExpiresAt = state == OrderStockAllocationState.Held ? now.AddMinutes(5) : null
        });
        await app.Db.SaveChangesAsync();

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("None", result.Conflict?.ExpectedState);
        Assert.Equal(state.ToString(), result.Conflict?.ExistingState);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task Manual_payment_commit_with_different_lines_after_commit_returns_conflict()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 2)]));

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 1)]));

        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("None", result.Conflict?.ExpectedState);
        Assert.Equal("Committed", result.Conflict?.ExistingState);
        Assert.Equal(1, Assert.Single(result.Conflict!.ExpectedLines).Quantity);
        Assert.Equal(2, Assert.Single(result.Conflict.ExistingLines).Quantity);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.CommittedFacts);
    }

    [Fact]
    public async Task PayOS_commit_transitions_held_allocation_to_committed_without_decrementing_stock_again()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.Held,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.True(result.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Committed, allocation.State);
        Assert.NotNull(allocation.CommittedAt);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));

        var committed = Assert.Single(app.CommittedFacts);
        Assert.Equal(orderId, committed.OrderId);
        Assert.Equal($"stock:{orderId}:committed", committed.IdempotencyKey);
        var line = Assert.Single(committed.Lines);
        Assert.Equal(item.VariantId, line.ProductVariantId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task PayOS_commit_with_mismatched_lines_returns_conflict_without_changing_hold()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.Held,
            [new StockLifecycleLine(item.VariantId, 1)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Held", result.Conflict?.ExpectedState);
        Assert.Equal("Held", result.Conflict?.ExistingState);
        Assert.Equal(1, Assert.Single(result.Conflict!.ExpectedLines).Quantity);
        Assert.Equal(2, Assert.Single(result.Conflict.ExistingLines).Quantity);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Held, allocation.State);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task PayOS_commit_with_missing_allocation_returns_conflict()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.Held,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Held", result.Conflict?.ExpectedState);
        Assert.Equal("Missing", result.Conflict?.ExistingState);
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task PayOS_commit_with_expired_released_allocation_returns_conflict_without_reconsuming_stock()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(-1)));
        await app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId));

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.Held,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Held", result.Conflict?.ExpectedState);
        Assert.Equal("Released", result.Conflict?.ExistingState);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Released, allocation.State);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.CommittedFacts);
    }

    [Fact]
    public async Task PayOS_commit_retry_after_commit_is_idempotent_without_duplicate_fact()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));

        var first = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.Held,
            [new StockLifecycleLine(item.VariantId, 2)]));
        var retry = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.Held,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Committed, allocation.State);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.CommittedFacts);
    }

    [Fact]
    public async Task PayOS_commit_may_commit_expired_hold_before_it_is_released()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(-1)));

        var result = await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.Held,
            [new StockLifecycleLine(item.VariantId, 2)]));

        Assert.True(result.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Committed, allocation.State);
        Assert.True(allocation.HoldExpiresAt < DateTime.UtcNow);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.CommittedFacts);
    }
}
