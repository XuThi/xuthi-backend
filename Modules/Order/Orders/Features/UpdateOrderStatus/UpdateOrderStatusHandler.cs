using Customer.Data;
using Customer.Customers.Models;
using ProductCatalog.Data;

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
    ProductCatalogDbContext catalogDb,
    CustomerDbContext customerDb)
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

            // Deduct customer spending stats and recalculate tier
            if (order.CustomerId.HasValue)
            {
                var customer = await customerDb.Customers
                    .FirstOrDefaultAsync(c => c.Id == order.CustomerId.Value, cancellationToken);

                if (customer is not null)
                {
                    customer.TotalSpent = Math.Max(0, customer.TotalSpent - order.Total);
                    customer.TotalOrders = Math.Max(0, customer.TotalOrders - 1);
                    customer.Tier = customer.TotalSpent switch
                    {
                        >= 10_000_000m => CustomerTier.Platinum,
                        >= 5_000_000m => CustomerTier.Gold,
                        >= 1_000_000m => CustomerTier.Silver,
                        _ => CustomerTier.Standard
                    };
                    await customerDb.SaveChangesAsync(cancellationToken);
                }
            }
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
}
