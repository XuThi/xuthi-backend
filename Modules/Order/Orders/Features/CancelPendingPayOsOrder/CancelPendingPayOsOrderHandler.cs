using ProductCatalog.Products.Services;

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
    IStockReservationService stockReservation)
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

        if (order.PaymentMethod != PaymentMethod.PayOS)
            throw new InvalidOperationException("Chỉ cho phép hủy đơn thanh toán chuyển khoản PayOS.");

        if (!IsOrderOwner(order, command.RequestUserId, command.RequestEmail))
            throw new UnauthorizedAccessException("Bạn không có quyền hủy đơn hàng này.");

        if (order.Status != OrderStatus.Pending || order.PaymentStatus != PaymentStatus.Pending)
            throw new InvalidOperationException("Đơn hàng không còn ở trạng thái chờ thanh toán để hủy.");

        order.Status = OrderStatus.Cancelled;
        order.PaymentStatus = PaymentStatus.Failed;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = string.IsNullOrWhiteSpace(command.Reason)
            ? "Khách hủy thanh toán PayOS"
            : command.Reason;

        if (!string.IsNullOrEmpty(order.ReservationSessionKey))
        {
            await stockReservation.ReleaseReservationsAsync(order.ReservationSessionKey, cancellationToken);
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
