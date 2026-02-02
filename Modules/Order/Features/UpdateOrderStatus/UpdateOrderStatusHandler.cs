using ProductCatalog.Infrastructure.Data;

namespace Order.Features.UpdateOrderStatus;

internal class UpdateOrderStatusHandler(
    OrderDbContext orderDb,
    ProductCatalogDbContext catalogDb)
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
            // Note: No stock restoration in simplified design
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
            order.UpdatedAt
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
