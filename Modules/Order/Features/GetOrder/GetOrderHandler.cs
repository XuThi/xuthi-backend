namespace Order.Features.GetOrder;

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
