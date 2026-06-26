using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductCatalog.Products.Services;

namespace ProductCatalog.Products.BackgroundServices;

/// <summary>
/// Periodically releases expired stock allocations (TTL = 5 minutes).
/// Runs every 30 seconds.
/// </summary>
public class StockReservationCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<StockReservationCleanupService> logger) : BackgroundService
{
    public static bool RequireCheck = true; // True by default so it checks on startup
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StockReservationCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!RequireCheck)
            {
                await Task.Delay(Interval, stoppingToken);
                continue;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IStockReservationService>();

                var released = await service.CleanupExpiredReservationsAsync(stoppingToken);
                if (released > 0)
                {
                    logger.LogInformation("Released {Count} expired stock allocations", released);
                }

                // Check if there are any remaining active allocations logic directly via dbContext
                var db = scope.ServiceProvider.GetRequiredService<ProductCatalog.Data.ProductCatalogDbContext>();
                var hasActive = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                    db.OrderStockAllocations, r => r.State == ProductCatalog.Products.Models.OrderStockAllocationState.Held, stoppingToken);
                
                if (!hasActive)
                {
                    RequireCheck = false;
                    logger.LogInformation("No active stock allocations remaining. Stock allocation cleanup paused until a new hold.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error cleaning up expired stock reservations");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
