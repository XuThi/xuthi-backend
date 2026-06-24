using ProductCatalog.Products.Services;
using Promotion.Vouchers.Features.ManageVoucherUsage;

namespace Order.Orders.Features.CancelPendingPayOsOrder;

public record CancelPendingPayOsOrderCommand(
    Guid OrderId,
    Guid? RequestUserId,
    string? RequestEmail,
    string? Reason = null
) : ICommand<CancelPendingPayOsOrderResult>;

public record CancelPendingPayOsOrderResult(
    Guid OrderId,
    string OrderNumber,
    string Status,
    string PaymentStatus,
    DateTime CancelledAt,
    string? CancellationReason
);

internal class CancelPendingPayOsOrderHandler(
    OrderDbContext orderDb,
    IStockReservationService stockReservation,
    ISender sender)
    : ICommandHandler<CancelPendingPayOsOrderCommand, CancelPendingPayOsOrderResult>
{
    public async Task<CancelPendingPayOsOrderResult> Handle(
        CancelPendingPayOsOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderDb.Orders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
            throw new KeyNotFoundException("Order not found");

        if (!IsOrderOwner(order, command.RequestUserId, command.RequestEmail))
            throw new UnauthorizedAccessException("Ban khong co quyen huy don hang nay.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Chi co the huy don hang truoc khi shop xac nhan.");

        if (order.PaymentMethod == PaymentMethod.PayOS && order.PaymentStatus != PaymentStatus.Pending)
            throw new InvalidOperationException("Don hang PayOS khong con o trang thai cho thanh toan de huy.");

        order.Status = OrderStatus.Cancelled;
        order.PaymentStatus = PaymentStatus.Failed;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = string.IsNullOrWhiteSpace(command.Reason)
            ? "Khach huy don hang truoc khi xac nhan"
            : command.Reason;

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

        await orderDb.SaveChangesAsync(cancellationToken);

        return new CancelPendingPayOsOrderResult(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.PaymentStatus.ToString(),
            order.CancelledAt ?? DateTime.UtcNow,
            order.CancellationReason
        );
    }

    private static bool IsOrderOwner(CustomerOrder order, Guid? requestUserId, string? requestEmail)
    {
        if (order.CustomerId.HasValue && requestUserId.HasValue && order.CustomerId.Value == requestUserId.Value)
            return true;

        if (!string.IsNullOrWhiteSpace(requestEmail)
            && string.Equals(order.CustomerEmail, requestEmail, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
