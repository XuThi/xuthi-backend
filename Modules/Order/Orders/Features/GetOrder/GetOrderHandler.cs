using Order.Orders.OrderIntake;

namespace Order.Orders.Features.GetOrder;

public record GetOrderQuery(Guid? Id = null, string? OrderNumber = null) : IQuery<OrderDetailResult>;

public record OrderDetailResult(
    Guid Id,
    string OrderNumber,
    Guid? SourceCartId,
    
    // Customer
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    
    // Shipping
    string ShippingAddress,
    string ShippingCity,
    string ShippingWard,
    string? ShippingNote,
    
    // Totals
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal Total,
    
    // Voucher
    string? VoucherCode,
    
    // Status
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    
    // Timestamps
    DateTime CreatedAt,
    DateTime? CreatedOrderAt,
    DateTime? PaidAt,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    
    // Items
    List<OrderItemDetail> Items
);

public record OrderItemDetail(
    Guid Id,
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantSku,
    string? VariantDescription,
    string? ImageUrl,
    decimal UnitPrice,
    decimal? CompareAtPrice,
    int Quantity,
    decimal TotalPrice
);

internal class GetOrderHandler(
    OrderDbContext dbContext,
    IOrderIntake orderIntake)
    : IQueryHandler<GetOrderQuery, OrderDetailResult>
{
    public async Task<OrderDetailResult> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        Guid? orderId = null;

        if (request.Id.HasValue)
        {
            orderId = await dbContext.Orders
                .Where(o => o.Id == request.Id.Value)
                .Select(o => (Guid?)o.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.OrderNumber))
        {
            orderId = await dbContext.Orders
                .Where(o => o.OrderNumber == request.OrderNumber)
                .Select(o => (Guid?)o.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!orderId.HasValue)
        {
            throw new KeyNotFoundException("Order not found");
        }

        await orderIntake.ExpirePayOsOrderAttemptIfSettlementGraceEndedAsync(
            orderId.Value,
            cancellationToken);

        var order = await dbContext.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == orderId.Value, cancellationToken);

        return new OrderDetailResult(
            order.Id,
            order.OrderNumber,
            order.SourceCartId,
            order.CustomerName,
            order.CustomerEmail,
            order.CustomerPhone,
            order.ShippingAddress,
            order.ShippingCity,
            order.ShippingWard,
            order.ShippingNote,
            order.Subtotal,
            order.DiscountAmount,
            order.ShippingFee,
            order.Total,
            order.VoucherCode,
            order.Status.ToString(),
            order.PaymentStatus.ToString(),
            order.PaymentMethod.ToString(),
            order.CreatedAt!.Value,
            order.CreatedOrderAt,
            order.PaidAt,
            order.ShippedAt,
            order.DeliveredAt,
            order.CancelledAt,
            order.CancellationReason,
            order.Items.Select(i => new OrderItemDetail(
                i.Id,
                i.ProductId,
                i.VariantId,
                i.ProductName,
                i.VariantSku,
                i.VariantDescription,
                i.ImageUrl,
                i.UnitPrice,
                i.CompareAtPrice,
                i.Quantity,
                i.TotalPrice
            )).ToList()
        );
    }
}
