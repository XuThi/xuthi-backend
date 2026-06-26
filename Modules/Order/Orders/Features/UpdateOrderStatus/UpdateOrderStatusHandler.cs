using ProductCatalog.Products.Services;

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
    IStockReservationService stockReservation,
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
            if (!string.IsNullOrEmpty(order.ReservationSessionKey))
            {
                await stockReservation.ReleaseReservationsAsync(order.ReservationSessionKey, cancellationToken);
                await stockReservation.RestoreConfirmedReservationsAsync(order.ReservationSessionKey, order.Id, cancellationToken);
            }
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
}
