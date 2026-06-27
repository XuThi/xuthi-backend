using Contracts;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Products.Models;

namespace XuThiWebApp.Tests;

public sealed class StockLifecycleReleaseTests
{
    [Fact]
    public async Task Releasing_order_attempt_stock_releases_held_allocations_restores_stock_and_records_fact()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));

        var result = await app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId));

        Assert.True(result.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Released, allocation.State);
        Assert.NotNull(allocation.ReleasedAt);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));

        var released = Assert.Single(app.ReleasedFacts);
        Assert.Equal(orderId, released.OrderId);
        Assert.Equal($"stock:{orderId}:released", released.IdempotencyKey);
        var line = Assert.Single(released.Lines);
        Assert.Equal(item.VariantId, line.ProductVariantId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task Releasing_already_released_order_attempt_stock_is_idempotent_without_duplicate_fact()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();

        await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));

        var first = await app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId));
        var retry = await app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Released, allocation.State);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.ReleasedFacts);
    }

    [Fact]
    public async Task Releasing_order_attempt_stock_with_no_allocation_returns_conflict()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var orderId = Guid.NewGuid();

        var result = await app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Held", result.Conflict?.ExpectedState);
        Assert.Equal("Missing", result.Conflict?.ExistingState);
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Empty(app.ReleasedFacts);
    }

    [Fact]
    public async Task Releasing_order_attempt_stock_when_variant_stock_cannot_be_updated_keeps_hold()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var orderId = Guid.NewGuid();
        var missingVariantId = Guid.NewGuid();

        app.Db.OrderStockAllocations.Add(new OrderStockAllocation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductVariantId = missingVariantId,
            Quantity = 2,
            State = OrderStockAllocationState.Held,
            HeldAt = DateTime.UtcNow,
            HoldExpiresAt = DateTime.UtcNow.AddMinutes(6)
        });
        await app.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId)));

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Held, allocation.State);
        Assert.Empty(app.ReleasedFacts);
    }

    [Theory]
    [InlineData(OrderStockAllocationState.Committed)]
    [InlineData(OrderStockAllocationState.Restored)]
    public async Task Releasing_order_attempt_stock_in_wrong_lifecycle_state_returns_conflict(
        OrderStockAllocationState state)
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 3);
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        app.Db.OrderStockAllocations.Add(new OrderStockAllocation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductVariantId = item.VariantId,
            Quantity = 2,
            State = state,
            HeldAt = now.AddMinutes(-5),
            CommittedAt = state == OrderStockAllocationState.Committed ? now.AddMinutes(-4) : now.AddMinutes(-5),
            RestoredAt = state == OrderStockAllocationState.Restored ? now.AddMinutes(-3) : null
        });
        await app.Db.SaveChangesAsync();

        var result = await app.Sender.Send(new ReleaseOrderAttemptStockCommand(orderId));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, result.Status);
        Assert.Equal("Held", result.Conflict?.ExpectedState);
        Assert.Equal(state.ToString(), result.Conflict?.ExistingState);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.ReleasedFacts);
    }
}
