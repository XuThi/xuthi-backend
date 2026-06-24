using Order.Orders.OrderIntake;

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

internal class CancelPendingPayOsOrderHandler(IOrderIntake orderIntake)
    : ICommandHandler<CancelPendingPayOsOrderCommand, CancelPendingPayOsOrderResult>
{
    public async Task<CancelPendingPayOsOrderResult> Handle(
        CancelPendingPayOsOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await orderIntake.CancelOrderAttemptAsync(new CancelOrderAttempt(
            command.OrderId,
            command.RequestUserId,
            command.RequestEmail,
            command.Reason), cancellationToken);

        return new CancelPendingPayOsOrderResult(
            result.OrderId,
            result.OrderNumber,
            result.Status,
            result.PaymentStatus,
            result.CancelledAt,
            result.CancellationReason
        );
    }
}
