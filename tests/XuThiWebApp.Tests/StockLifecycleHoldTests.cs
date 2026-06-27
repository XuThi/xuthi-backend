using Contracts;
using Core.Extensions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog;
using ProductCatalog.Brands.Models;
using ProductCatalog.Categories.Models;
using ProductCatalog.Data;
using ProductCatalog.Products.Models;
using System.Text.Json;

namespace XuThiWebApp.Tests;

public sealed class StockLifecycleHoldTests
{
    [Fact]
    public async Task Holding_order_attempt_stock_creates_held_allocation_decrements_stock_and_records_fact()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();
        var holdExpiresAt = DateTime.UtcNow.AddMinutes(6);

        var result = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            holdExpiresAt));

        Assert.True(result.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(orderId, allocation.OrderId);
        Assert.Equal(item.VariantId, allocation.ProductVariantId);
        Assert.Equal(2, allocation.Quantity);
        Assert.Equal(OrderStockAllocationState.Held, allocation.State);
        Assert.Null(allocation.LegacySessionKey);
        Assert.NotNull(allocation.HeldAt);
        Assert.Equal(holdExpiresAt, allocation.HoldExpiresAt);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));

        var held = Assert.Single(app.HeldFacts);
        Assert.Equal(orderId, held.OrderId);
        Assert.Equal($"stock:{orderId}:held", held.IdempotencyKey);
        var line = Assert.Single(held.Lines);
        Assert.Equal(item.VariantId, line.ProductVariantId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task Holding_order_attempt_stock_reports_insufficient_stock_without_partial_changes()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 1);

        var result = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            Guid.NewGuid(),
            [new StockLifecycleLine(item.VariantId, 2)],
            DateTime.UtcNow.AddMinutes(6)));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.InsufficientStock, result.Status);

        var detail = Assert.Single(result.InsufficientStockDetails);
        Assert.Equal(item.VariantId, detail.ProductVariantId);
        Assert.Equal(2, detail.RequestedQuantity);
        Assert.Equal(1, detail.AvailableQuantity);
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(1, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.HeldFacts);
    }

    [Fact]
    public async Task Holding_order_attempt_stock_rejects_missing_deleted_or_inactive_variants()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var inactive = await app.SeedCatalogItemAsync(
            stockQuantity: 5,
            configureVariant: variant => variant.IsActive = false);
        var deleted = await app.SeedCatalogItemAsync(
            stockQuantity: 5,
            configureVariant: variant => variant.IsDeleted = true);
        var missingVariantId = Guid.NewGuid();

        var result = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            Guid.NewGuid(),
            [
                new StockLifecycleLine(inactive.VariantId, 1),
                new StockLifecycleLine(deleted.VariantId, 1),
                new StockLifecycleLine(missingVariantId, 1)
            ],
            DateTime.UtcNow.AddMinutes(6)));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.ValidationFailed, result.Status);
        Assert.Equal(
            new[] { inactive.VariantId, deleted.VariantId, missingVariantId }.OrderBy(id => id),
            result.ValidationDetails
                .Select(detail => detail.ProductVariantId!.Value)
                .OrderBy(id => id));
        Assert.All(result.ValidationDetails, detail =>
            Assert.Equal("ProductVariantUnavailable", detail.Code));
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(5, await app.GetStockQuantityAsync(inactive.VariantId));
        Assert.Equal(5, await app.GetStockQuantityAsync(deleted.VariantId));
        Assert.Empty(app.HeldFacts);
    }

    [Fact]
    public async Task Holding_order_attempt_stock_rejects_zero_or_negative_quantities()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);

        var result = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            Guid.NewGuid(),
            [
                new StockLifecycleLine(item.VariantId, 0),
                new StockLifecycleLine(Guid.NewGuid(), -1)
            ],
            DateTime.UtcNow.AddMinutes(6)));

        Assert.False(result.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.ValidationFailed, result.Status);
        Assert.Equal(2, result.ValidationDetails.Count);
        Assert.All(result.ValidationDetails, detail =>
            Assert.Equal("QuantityMustBePositive", detail.Code));
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Empty(app.HeldFacts);
    }

    [Fact]
    public async Task Holding_same_order_attempt_stock_with_same_lines_is_idempotent()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 10);
        var orderId = Guid.NewGuid();
        var holdExpiresAt = DateTime.UtcNow.AddMinutes(10);

        var first = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [
                new StockLifecycleLine(item.VariantId, 2),
                new StockLifecycleLine(item.VariantId, 3)
            ],
            holdExpiresAt));
        var retry = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 5)],
            holdExpiresAt.AddMinutes(-1)));

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(orderId, allocation.OrderId);
        Assert.Equal(item.VariantId, allocation.ProductVariantId);
        Assert.Equal(5, allocation.Quantity);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.HeldFacts);
    }

    [Fact]
    public async Task Concurrent_same_order_attempt_stock_holds_are_idempotent_without_double_decrementing_stock()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 10);
        var orderId = Guid.NewGuid();
        var holdExpiresAt = DateTime.UtcNow.AddMinutes(10);
        var command = new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            holdExpiresAt);

        var results = await Task.WhenAll(
            app.SendInNewScopeAsync(command),
            app.SendInNewScopeAsync(command));

        Assert.All(results, result => Assert.True(result.IsSuccess));

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(orderId, allocation.OrderId);
        Assert.Equal(item.VariantId, allocation.ProductVariantId);
        Assert.Equal(2, allocation.Quantity);
        Assert.Equal(8, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.HeldFacts);
    }

    [Fact]
    public async Task Holding_same_order_attempt_stock_with_different_lines_or_later_expiry_conflicts()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();
        var holdExpiresAt = DateTime.UtcNow.AddMinutes(6);

        var first = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            holdExpiresAt));
        var laterExpiry = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 2)],
            holdExpiresAt.AddMinutes(1)));
        var differentLines = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            orderId,
            [new StockLifecycleLine(item.VariantId, 1)],
            holdExpiresAt.AddMinutes(-1)));

        Assert.True(first.IsSuccess);
        Assert.Equal(StockLifecycleResultStatus.Conflict, laterExpiry.Status);
        Assert.Equal(StockLifecycleResultStatus.Conflict, differentLines.Status);
        Assert.Equal("Held", laterExpiry.Conflict?.ExistingState);
        Assert.Equal("Held", differentLines.Conflict?.ExistingState);

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(2, allocation.Quantity);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
        Assert.Single(app.HeldFacts);
    }

    [Fact]
    public async Task Holding_order_attempt_stock_is_whole_order_atomic_when_one_line_fails()
    {
        await using var app = new ProductCatalogStockLifecycleTestApp();
        var available = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var shortStock = await app.SeedCatalogItemAsync(stockQuantity: 1);

        var result = await app.Sender.Send(new HoldOrderAttemptStockCommand(
            Guid.NewGuid(),
            [
                new StockLifecycleLine(available.VariantId, 2),
                new StockLifecycleLine(shortStock.VariantId, 2)
            ],
            DateTime.UtcNow.AddMinutes(6)));

        Assert.Equal(StockLifecycleResultStatus.InsufficientStock, result.Status);
        Assert.Empty(await app.Db.OrderStockAllocations.ToListAsync());
        Assert.Equal(5, await app.GetStockQuantityAsync(available.VariantId));
        Assert.Equal(1, await app.GetStockQuantityAsync(shortStock.VariantId));
        Assert.Empty(app.HeldFacts);
    }
}

internal sealed class ProductCatalogStockLifecycleTestApp : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;

    public ProductCatalogStockLifecycleTestApp()
    {
        _connectionString = $"Data Source=file:stock-lifecycle-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ProductCatalogDbContext>(options =>
            options.UseSqlite(_connectionString));
        services.AddMediatRWithAssemblies(typeof(ProductCatalogModule).Assembly);

        _provider = services.BuildServiceProvider(validateScopes: true);
        _scope = _provider.CreateAsyncScope();
        Db.Database.EnsureCreated();
    }

    public ISender Sender => _scope.ServiceProvider.GetRequiredService<ISender>();

    public ProductCatalogDbContext Db => _scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();

    public IReadOnlyList<OrderStockHeld> HeldFacts
        => Db.OrderStockLifecycleEventFacts
            .AsNoTracking()
            .Where(fact => fact.EventType == nameof(OrderStockHeld))
            .OrderBy(fact => fact.OccurredAt)
            .ToList()
            .Select(fact => new OrderStockHeld(
                fact.OrderId,
                JsonSerializer.Deserialize<List<StockLifecycleLine>>(fact.LinesJson) ?? [],
                fact.OccurredAt,
                fact.IdempotencyKey))
            .ToList();

    public IReadOnlyList<OrderStockHoldReleased> ReleasedFacts
        => Db.OrderStockLifecycleEventFacts
            .AsNoTracking()
            .Where(fact => fact.EventType == nameof(OrderStockHoldReleased))
            .OrderBy(fact => fact.OccurredAt)
            .ToList()
            .Select(fact => new OrderStockHoldReleased(
                fact.OrderId,
                JsonSerializer.Deserialize<List<StockLifecycleLine>>(fact.LinesJson) ?? [],
                fact.OccurredAt,
                fact.IdempotencyKey))
            .ToList();

    public async Task<CatalogStockItem> SeedCatalogItemAsync(
        int stockQuantity,
        Action<Variant>? configureVariant = null)
    {
        var index = Guid.NewGuid().ToString("N");
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = $"Stock lifecycle brand {index}",
            UrlSlug = $"stock-lifecycle-brand-{index}"
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = $"Stock lifecycle category {index}",
            UrlSlug = $"stock-lifecycle-category-{index}"
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Stock lifecycle product",
            UrlSlug = $"stock-lifecycle-product-{index}",
            Description = "Product seeded for stock lifecycle tests",
            BrandId = brand.Id,
            CategoryId = category.Id,
            IsActive = true,
            IsDeleted = false
        };
        var variant = new Variant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Sku = $"LIFE-{index}",
            Description = "Stock lifecycle variant",
            Price = 100m,
            StockQuantity = stockQuantity,
            IsActive = true,
            IsDeleted = false
        };
        configureVariant?.Invoke(variant);

        Db.Brands.Add(brand);
        Db.Categories.Add(category);
        Db.Products.Add(product);
        Db.Variants.Add(variant);
        await Db.SaveChangesAsync();

        return new CatalogStockItem(product.Id, variant.Id);
    }

    public async Task<int> GetStockQuantityAsync(Guid variantId)
    {
        return await Db.Variants
            .Where(v => v.Id == variantId)
            .Select(v => v.StockQuantity)
            .SingleAsync();
    }

    public async Task<StockLifecycleResult> SendInNewScopeAsync(
        HoldOrderAttemptStockCommand command,
        CancellationToken ct = default)
    {
        await using var scope = _provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
