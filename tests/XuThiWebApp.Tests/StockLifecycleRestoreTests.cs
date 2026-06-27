using Contracts;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Products.Models;

namespace XuThiWebApp.Tests;

public sealed class StockLifecycleRestoreTests
{
    [Fact]
    public async Task Restoring_created_order_stock_restores_committed_allocation_stock_and_records_fact()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 2)]));

        var result = await app.Sender.Send(new RestoreCreatedOrderStockCommand(orderId));

        Assert.True(result.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Restored, allocation.State);
        Assert.NotNull(allocation.RestoredAt);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));

        var restored = Assert.Single(app.RestoredFacts);
        Assert.Equal(orderId, restored.OrderId);
        Assert.Equal($"stock:{orderId}:restored", restored.IdempotencyKey);
        var line = Assert.Single(restored.Lines);
        Assert.Equal(item.VariantId, line.ProductVariantId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task Restoring_already_restored_created_order_stock_is_idempotent_without_duplicate_fact()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new CommitOrderStockCommand(
            orderId,
            StockLifecycleExpectedPriorState.None,
            [new StockLifecycleLine(item.VariantId, 2)]));

        var first = await app.Sender.Send(new RestoreCreatedOrderStockCommand(orderId));
        var retry = await app.Sender.Send(new RestoreCreatedOrderStockCommand(orderId));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Restored, allocation.State);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.RestoredFacts);
    }

    [Fact]
    public async Task Restoring_created_order_stock_with_no_allocation_returns_conflict()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var orderId = Guid.NewGuid();

        var result = await app.Sender.Send(new RestoreCreatedOrderStockCommand(orderId));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Committed", result.Conflict?.ExpectedState);
        Assert.Equal("Missing", result.Conflict?.ExistingState);
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Empty(app.RestoredFacts);
    }

    [Fact]
    public async Task Restoring_held_order_attempt_stock_returns_conflict_without_changing_stock()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));

        var result = await app.Sender.Send(new RestoreCreatedOrderStockCommand(orderId));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Committed", result.Conflict?.ExpectedState);
        Assert.Equal("Held", result.Conflict?.ExistingState);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Held, allocation.State);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.RestoredFacts);
    }

    [Fact]
    public async Task Restoring_released_order_attempt_stock_returns_conflict_without_changing_stock()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));
        await app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId));

        var result = await app.Sender.Send(new RestoreCreatedOrderStockCommand(orderId));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Committed", result.Conflict?.ExpectedState);
        Assert.Equal("Released", result.Conflict?.ExistingState);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Released, allocation.State);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.RestoredFacts);
    }
}
