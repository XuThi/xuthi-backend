namespace Order.Features.GetOrder;

public record GetOrderQuery(Guid? Id = null, string? OrderNumber = null) : IQuery<OrderDetailResult>;

public record OrderDetailResult(
    Guid Id,
    string OrderNumber,
    
    // Customer
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    
    // Shipping
    string ShippingAddress,
    string ShippingCity,
    string ShippingDistrict,
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

internal class GetOrderHandler(OrderDbContext dbContext)
    : IQueryHandler<GetOrderQuery, OrderDetailResult>
{
    public async Task<OrderDetailResult> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Orders
            .Include(o => o.Items)
            .AsQueryable();

        CustomerOrder? order = null;

        if (request.Id.HasValue)
        {
            order = await query.FirstOrDefaultAsync(o => o.Id == request.Id.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.OrderNumber))
        {
            order = await query.FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber, cancellationToken);
        }

        if (order is null)
        {
            throw new KeyNotFoundException("Order not found");
        }

        return new OrderDetailResult(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CustomerEmail,
            order.CustomerPhone,
            order.ShippingAddress,
            order.ShippingCity,
            order.ShippingDistrict,
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
            order.CreatedAt,
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
