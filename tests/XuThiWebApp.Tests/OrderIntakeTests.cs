using Cart.ShoppingCarts.Features.AddItemIntoCart;
using Cart.ShoppingCarts.Features.ApplyVoucher;
using Order.Orders.Features.Checkout;
using Order.Orders.Features.CancelPendingPayOsOrder;
using Order.Orders.Features.GetOrder;
using Order.Orders.Models;
using Promotion.Vouchers.Features.ManageVoucherUsage;
using Promotion.Vouchers.Features.ValidateVoucher;
using Promotion.Vouchers.Models;

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

    [Fact]
    public async Task Manual_payment_checkout_finalizes_voucher_usage_for_the_created_order()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "INTAKE10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "manual-voucher-hold-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("INTAKE10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "intake10"));

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

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);

        Assert.Equal(checkout.OrderId, usage.OrderId);
        Assert.Equal(VoucherUsageStatus.Finalized, usage.Status);
        Assert.NotNull(usage.FinalizedAt);
    }

    [Fact]
    public async Task PayOS_checkout_holds_voucher_usage_until_customer_cancels_order_attempt()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYHOLD10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-voucher-hold-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYHOLD10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "payhold10"));

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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        var heldAudit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var heldUsage = Assert.Single(heldAudit.Usages);
        Assert.Equal(VoucherUsageStatus.Held, heldUsage.Status);

        var heldCapacity = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "PAYHOLD10",
            CartQuoteAmount: 100m));
        Assert.False(heldCapacity.IsValid);

        await app.Sender.Send(new CancelPendingPayOsOrderCommand(
            checkout.OrderId,
            RequestUserId: null,
            RequestEmail: "jane@example.com",
            Reason: "Changed mind"));

        var releasedCapacity = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "PAYHOLD10",
            CartQuoteAmount: 100m));
        Assert.True(releasedCapacity.IsValid, releasedCapacity.ErrorMessage);

        var releasedAudit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var releasedUsage = Assert.Single(releasedAudit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, releasedUsage.Status);
        Assert.NotNull(releasedUsage.ReleasedAt);
    }
}
