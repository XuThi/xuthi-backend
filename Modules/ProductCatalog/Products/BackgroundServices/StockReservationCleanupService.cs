using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductCatalog.Products.Services;

namespace ProductCatalog.Products.BackgroundServices;

/// <summary>
/// Periodically releases expired stock reservations (TTL = 5 minutes).
/// Runs every 30 seconds.
/// </summary>
public class StockReservationCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<StockReservationCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StockReservationCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IStockReservationService>();

                var released = await service.CleanupExpiredReservationsAsync(stoppingToken);
                if (released > 0)
                {
                    logger.LogInformation("Released {Count} expired stock reservations", released);
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
