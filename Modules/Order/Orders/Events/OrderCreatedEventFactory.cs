namespace Order.Orders.Events;

internal static class OrderCreatedEventFactory
{
    public static OrderCreatedEvent FromOrder(CustomerOrder order)
    {
        return new OrderCreatedEvent(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CustomerEmail,
            order.CustomerPhone,
            order.ShippingAddress,
            order.ShippingCity,
            order.ShippingWard,
            order.Subtotal,
            order.DiscountAmount,
            order.ShippingFee,
            order.Total,
            order.Items.Select(i => new OrderCreatedEventItem(
                i.ProductName,
                i.VariantDescription,
                i.Quantity,
                i.TotalPrice
            )).ToList()
        );
    }
}