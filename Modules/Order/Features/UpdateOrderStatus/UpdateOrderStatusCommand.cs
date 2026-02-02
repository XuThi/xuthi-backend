namespace Order.Features.UpdateOrderStatus;

public record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus NewStatus,
    string? Reason = null // For cancellation
) : ICommand<UpdateOrderStatusResult>;

public record UpdateOrderStatusResult(
    Guid OrderId,
    string OrderNumber,
    string PreviousStatus,
    string NewStatus,
    DateTime UpdatedAt
);
