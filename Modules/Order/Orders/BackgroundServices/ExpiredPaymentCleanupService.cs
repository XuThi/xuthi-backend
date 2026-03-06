using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Order.Data;
using Order.Orders.Models;
using ProductCatalog.Products.Services;

namespace Order.Orders.BackgroundServices;

/// <summary>
/// Periodically cancels orders with expired PayOS payment links.
/// PayOS links expire after 15 minutes. This service checks every 60 seconds
/// for pending-payment PayOS orders older than 15 minutes and cancels them,
/// releasing stock reservations.
/// </summary>
public class ExpiredPaymentCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredPaymentCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PaymentExpiry = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ExpiredPaymentCleanupService started (expiry={Expiry}min, interval={Interval}s)",
            PaymentExpiry.TotalMinutes, CheckInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error cleaning up expired payment orders");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredOrdersAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var stockReservation = scope.ServiceProvider.GetRequiredService<IStockReservationService>();

        var cutoff = DateTime.UtcNow - PaymentExpiry;

        // Find PayOS orders that are still pending payment and were created before the cutoff
        var expiredOrders = await db.Orders
            .Where(o => o.PaymentMethod == PaymentMethod.PayOS
                     && o.PaymentStatus == PaymentStatus.Pending
                     && o.Status == OrderStatus.Pending
                     && o.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (expiredOrders.Count == 0) return;

        foreach (var order in expiredOrders)
        {
            order.Status = OrderStatus.Cancelled;
            order.PaymentStatus = PaymentStatus.Failed;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = "Quá thời gian thanh toán (15 phút)";

            // Release stock reservation
            if (!string.IsNullOrEmpty(order.ReservationSessionKey))
            {
                try
                {
                    await stockReservation.ReleaseReservationsAsync(order.ReservationSessionKey, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to release stock for expired order {OrderNumber}", order.OrderNumber);
                }
            }

            logger.LogInformation(
                "Cancelled expired PayOS order {OrderNumber} (created at {CreatedAt})",
                order.OrderNumber, order.CreatedAt);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Cancelled {Count} expired PayOS payment orders", expiredOrders.Count);
    }
}
