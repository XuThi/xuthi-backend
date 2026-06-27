using Contracts;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Products.Models;

namespace XuThiWebApp.Tests;

public sealed class StockLifecycleCommitTests
{
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
