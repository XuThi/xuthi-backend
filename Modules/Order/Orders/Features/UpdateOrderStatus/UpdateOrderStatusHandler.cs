using Customer.Data;
using Customer.Customers.Features.AddCustomerOrder;
using Customer.Customers.Models;
using ProductCatalog.Products.Services;
using Promotion.Vouchers.Features.ManageVoucherUsage;

namespace Order.Orders.Features.UpdateOrderStatus;

public record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus NewStatus,
    string? Reason = null
) : ICommand<UpdateOrderStatusResult>;

public record UpdateOrderStatusResult(
    Guid OrderId,
    string OrderNumber,
    string PreviousStatus,
    string NewStatus,
    DateTime UpdatedAt
);

internal class UpdateOrderStatusHandler(
    OrderDbContext orderDb,
    CustomerDbContext customerDb,
    IStockReservationService stockReservation,
    ISender sender)
    : ICommandHandler<UpdateOrderStatusCommand, UpdateOrderStatusResult>
{
    public async Task<UpdateOrderStatusResult> Handle(UpdateOrderStatusCommand command, CancellationToken cancellationToken)
    {
        var order = await orderDb.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
            throw new KeyNotFoundException("Order not found");

        var previousStatus = order.Status;

        // Validate status transition
        ValidateStatusTransition(previousStatus, command.NewStatus);

        // Handle cancellation
        if (command.NewStatus == OrderStatus.Cancelled)
        {
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = command.Reason;

            if (!string.IsNullOrEmpty(order.ReservationSessionKey))
            {
                await stockReservation.ReleaseReservationsAsync(order.ReservationSessionKey, cancellationToken);
                await stockReservation.RestoreConfirmedReservationsAsync(order.ReservationSessionKey, order.Id, cancellationToken);
            }

            if (order.VoucherId.HasValue && order.CreatedOrderAt is null)
            {
                await sender.Send(new ReleaseVoucherUsageCommand(
                    order.VoucherId.Value,
                    order.Id), cancellationToken);
            }

            await ReverseCustomerOrderStatsIfAwarded(order, cancellationToken);
        }

        // Update timestamps based on new status
        switch (command.NewStatus)
        {
            case OrderStatus.Confirmed:
                // Order confirmed by admin
                break;
            case OrderStatus.Shipped:
                order.ShippedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Delivered:
                order.DeliveredAt = DateTime.UtcNow;
                // Mark as paid for COD
                if (order.PaymentMethod == PaymentMethod.CashOnDelivery)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.PaidAt = DateTime.UtcNow;
                }
                break;
        }

        order.Status = command.NewStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await orderDb.SaveChangesAsync(cancellationToken);

        if (command.NewStatus == OrderStatus.Delivered
            && order.PaymentStatus == PaymentStatus.Paid
            && order.CustomerId.HasValue)
        {
            var pointsEarned = (int)(order.Total / 10000);

            await sender.Send(new AddCustomerOrderCommand(
                order.CustomerId.Value,
                order.Total,
                pointsEarned,
                order.Id
            ), cancellationToken);
        }

        return new UpdateOrderStatusResult(
            order.Id,
            order.OrderNumber,
            previousStatus.ToString(),
            command.NewStatus.ToString(),
            order.UpdatedAt!.Value
        );
    }

    private static void ValidateStatusTransition(OrderStatus current, OrderStatus target)
    {
        // Define valid transitions
        var validTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
            [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Returned],
            [OrderStatus.Delivered] = [OrderStatus.Returned],
            [OrderStatus.Cancelled] = [], // Terminal state
            [OrderStatus.Returned] = [] // Terminal state
        };

        if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(target))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {current} to {target}");
        }
    }

    private async Task ReverseCustomerOrderStatsIfAwarded(CustomerOrder order, CancellationToken ct)
    {
        if (!order.CustomerId.HasValue)
            return;

        var customer = await customerDb.Customers
            .FirstOrDefaultAsync(c => c.Id == order.CustomerId.Value, ct);

        if (customer is null)
            return;

        var earnedHistory = await customerDb.PointsHistory
            .FirstOrDefaultAsync(h => h.RelatedOrderId == order.Id && h.Type == PointsTransactionType.Earned, ct);

        if (earnedHistory is null)
            return;

        customer.TotalSpent = Math.Max(0, customer.TotalSpent - order.Total);
        customer.TotalOrders = Math.Max(0, customer.TotalOrders - 1);
        customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - earnedHistory.Points);
        customer.Tier = customer.TotalSpent switch
        {
            >= 10_000_000m => CustomerTier.Platinum,
            >= 5_000_000m => CustomerTier.Gold,
            >= 1_000_000m => CustomerTier.Silver,
            _ => CustomerTier.Standard
        };
        customer.UpdatedAt = DateTime.UtcNow;

        customerDb.PointsHistory.Add(new PointsHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = PointsTransactionType.Adjusted,
            Points = -earnedHistory.Points,
            BalanceAfter = customer.LoyaltyPoints,
            Description = "Order cancelled",
            RelatedOrderId = order.Id
        });

        await customerDb.SaveChangesAsync(ct);
    }
}
