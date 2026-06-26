using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProductCatalog.Brands.Models;
using ProductCatalog.Categories.Models;
using ProductCatalog.Data;
using ProductCatalog.Products.Models;
using ProductCatalog.Products.Services;

namespace XuThiWebApp.Tests;

public sealed class OrderStockAllocationStorageTests
{
    [Fact]
    public async Task Reserving_stock_for_order_session_persists_held_allocation_and_decrements_stock()
    {
        await using var app = new ProductCatalogStockTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();
        var sessionKey = $"order:{orderId}";

        var reservationIds = await app.Stock.ReserveStockAsync(
            sessionKey,
            [(item.VariantId, 2)],
            TimeSpan.FromMinutes(6));

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Single(reservationIds);
        Assert.Equal(orderId, allocation.OrderId);
        Assert.Equal(item.VariantId, allocation.ProductVariantId);
        Assert.Equal(2, allocation.Quantity);
        Assert.Equal(OrderStockAllocationState.Held, allocation.State);
        Assert.Equal(sessionKey, allocation.LegacySessionKey);
        Assert.NotNull(allocation.HeldAt);
        Assert.Null(allocation.CommittedAt);
        Assert.Null(allocation.ReleasedAt);
        Assert.Null(allocation.RestoredAt);
        Assert.NotNull(allocation.HoldExpiresAt);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));
    }

    [Fact]
    public async Task Repeated_lines_for_the_same_variant_create_one_allocation_line()
    {
        await using var app = new ProductCatalogStockTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 10);
        var orderId = Guid.NewGuid();

        await app.Stock.ReserveStockAsync(
            $"order:{orderId}",
            [(item.VariantId, 2), (item.VariantId, 3)],
            TimeSpan.FromMinutes(6));

        var allocation = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(orderId, allocation.OrderId);
        Assert.Equal(item.VariantId, allocation.ProductVariantId);
        Assert.Equal(5, allocation.Quantity);
        Assert.Equal(OrderStockAllocationState.Held, allocation.State);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
    }

    [Fact]
    public async Task Storage_rejects_duplicate_order_variant_allocation_lines()
    {
        await using var app = new ProductCatalogStockTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 10);
        var orderId = Guid.NewGuid();

        await app.Stock.ReserveStockAsync(
            $"order:{orderId}",
            [(item.VariantId, 2)],
            TimeSpan.FromMinutes(6));

        app.Db.OrderStockAllocations.Add(new OrderStockAllocation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductVariantId = item.VariantId,
            Quantity = 2,
            State = OrderStockAllocationState.Held,
            HeldAt = DateTime.UtcNow,
            HoldExpiresAt = DateTime.UtcNow.AddMinutes(6),
            LegacySessionKey = $"order:{orderId}"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => app.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Confirmed_allocations_restore_to_restored_without_double_incrementing_stock()
    {
        await using var app = new ProductCatalogStockTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();
        var sessionKey = $"order:{orderId}";

        await app.Stock.ReserveStockAsync(sessionKey, [(item.VariantId, 2)], TimeSpan.FromMinutes(6));
        await app.Stock.ConfirmReservationsAsync(sessionKey, orderId);

        var committed = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Committed, committed.State);
        Assert.NotNull(committed.CommittedAt);
        Assert.Equal(3, await app.GetStockQuantityAsync(item.VariantId));

        await app.Stock.RestoreConfirmedReservationsAsync(sessionKey, orderId);
        await app.Stock.RestoreConfirmedReservationsAsync(sessionKey, orderId);

        var restored = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Restored, restored.State);
        Assert.NotNull(restored.RestoredAt);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
    }

    [Fact]
    public async Task Released_held_allocations_increment_stock_once()
    {
        await using var app = new ProductCatalogStockTestApp();
        var item = await app.SeedCatalogItemAsync(stockQuantity: 5);
        var orderId = Guid.NewGuid();
        var sessionKey = $"order:{orderId}";

        await app.Stock.ReserveStockAsync(sessionKey, [(item.VariantId, 2)], TimeSpan.FromMinutes(6));

        await app.Stock.ReleaseReservationsAsync(sessionKey);
        await app.Stock.ReleaseReservationsAsync(sessionKey);

        var released = await app.Db.OrderStockAllocations
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(OrderStockAllocationState.Released, released.State);
        Assert.NotNull(released.ReleasedAt);
        Assert.Equal(5, await app.GetStockQuantityAsync(item.VariantId));
    }
}

internal sealed class ProductCatalogStockTestApp : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ProductCatalogDbContext _db;

    public ProductCatalogStockTestApp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ProductCatalogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ProductCatalogDbContext(options);
        _db.Database.EnsureCreated();
        Stock = new StockReservationService(
            _db,
            NullLogger<StockReservationService>.Instance);
    }

    public ProductCatalogDbContext Db => _db;

    public StockReservationService Stock { get; }

    public async Task<CatalogStockItem> SeedCatalogItemAsync(int stockQuantity)
    {
        var index = Guid.NewGuid().ToString("N");
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = $"Stock test brand {index}",
            UrlSlug = $"stock-test-brand-{index}"
        };
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = $"Stock test category {index}",
            UrlSlug = $"stock-test-category-{index}"
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Stock test product",
            UrlSlug = $"stock-test-product-{index}",
            Description = "Product seeded for stock allocation tests",
            BrandId = brand.Id,
            CategoryId = category.Id,
            IsActive = true,
            IsDeleted = false
        };
        var variant = new Variant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Sku = $"STOCK-{index}",
            Description = "Stock test variant",
            Price = 100m,
            StockQuantity = stockQuantity,
            IsActive = true,
            IsDeleted = false
        };

        _db.Brands.Add(brand);
        _db.Categories.Add(category);
        _db.Products.Add(product);
        _db.Variants.Add(variant);
        await _db.SaveChangesAsync();

        return new CatalogStockItem(product.Id, variant.Id);
    }

    public async Task<int> GetStockQuantityAsync(Guid variantId)
    {
        return await _db.Variants
            .Where(v => v.Id == variantId)
            .Select(v => v.StockQuantity)
            .SingleAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed record CatalogStockItem(Guid ProductId, Guid VariantId);
