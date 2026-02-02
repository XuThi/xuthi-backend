namespace Order.Features.GetOrders;

public record GetOrdersQuery(
    string? Email = null,
    OrderStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<GetOrdersResult>;

public record GetOrdersResult(
    List<OrderSummary> Orders,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record OrderSummary(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    int ItemCount,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    DateTime CreatedAt
);
