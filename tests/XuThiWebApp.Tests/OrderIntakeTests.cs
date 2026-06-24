using Cart.ShoppingCarts.Features.AddItemIntoCart;
using Order.Orders.Features.Checkout;
using Order.Orders.Features.GetOrder;
using Order.Orders.Models;

namespace XuThiWebApp.Tests;

public sealed class OrderIntakeTests
{
    [Fact]
    public async Task BankTransfer_checkout_becomes_created_order_and_commits_stock_immediately()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "bank-transfer-created-order-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerId: null,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.BankTransfer)));

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));

        Assert.Equal(addResult.CartId, order.SourceCartId);
        Assert.NotNull(order.CreatedOrderAt);
        Assert.Equal("BankTransfer", order.PaymentMethod);
        Assert.Equal("Pending", order.Status);
        Assert.Null(checkout.PaymentUrl);
        Assert.Empty(app.PaymentLinkAttempts);

        var confirmation = Assert.Single(app.StockConfirmations);
        Assert.Equal(checkout.OrderId, confirmation.OrderId);
    }
}
