using Contracts;

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
    ISender sender,
    TimeProvider timeProvider)
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

        if (order.CreatedOrderAt is null)
            throw new InvalidOperationException(
                "Uncreated Order Attempts are owned by Order Intake and cannot be updated through the broader order-status workflow.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        order.ChangeStatus(command.NewStatus, now, command.Reason);

        if (command.NewStatus == OrderStatus.Cancelled)
        {
            var restoreResult = await sender.Send(
                new RestoreCreatedOrderStockCommand(order.Id),
                cancellationToken);
            EnsureStockLifecycleSuccess(
                restoreResult,
                "restore this Created Order's Stock Commitment");
        }

        await orderDb.SaveChangesAsync(cancellationToken);

        return new UpdateOrderStatusResult(
            order.Id,
            order.OrderNumber,
            previousStatus.ToString(),
            command.NewStatus.ToString(),
            order.UpdatedAt!.Value
        );
    }

    private static void EnsureStockLifecycleSuccess(
        StockLifecycleResult result,
        string operationDescription)
    {
        if (result.IsSuccess)
            return;

        var message = result.Status switch
        {
            StockLifecycleResultStatus.ValidationFailed => string.Join(
                " ",
                result.ValidationDetails.Select(detail => detail.Message)),
            StockLifecycleResultStatus.InsufficientStock => string.Join(
                " ",
                result.InsufficientStockDetails.Select(detail =>
                    $"Insufficient stock for Product Variant {detail.ProductVariantId}. Requested {detail.RequestedQuantity}, available {detail.AvailableQuantity}.")),
            StockLifecycleResultStatus.Conflict => result.Conflict?.Reason
                ?? "Stock lifecycle conflict for this Created Order.",
            _ => $"Stock lifecycle could not {operationDescription}."
        };

        throw new InvalidOperationException(message);
    }
}
