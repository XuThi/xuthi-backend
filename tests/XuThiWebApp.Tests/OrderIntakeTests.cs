using Cart.ShoppingCarts.Features.AddItemIntoCart;
using Cart.ShoppingCarts.Features.ApplyVoucher;
using Cart.ShoppingCarts.Models;
using Order.Orders.Features.CancelPendingPayOsOrder;
using Order.Orders.Features.Checkout;
using Order.Orders.Features.GetOrder;
using Order.Orders.Features.GetOrders;
using Order.Orders.Features.PayOsWebhook;
using Order.Orders.Features.UpdateOrderStatus;
using Order.Orders.Models;
using Order.Orders.OrderIntake;
using Microsoft.EntityFrameworkCore;
using Promotion.Vouchers.Features.ManageVoucherUsage;
using Promotion.Vouchers.Features.ValidateVoucher;
using Promotion.Vouchers.Models;

namespace XuThiWebApp.Tests;

public sealed class OrderIntakeTests
{
    [Fact]
    public async Task PayOS_paid_result_confirms_order_attempt_into_created_order()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYPAID10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-paid-result-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYPAID10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paypaid10"));

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

        var resolved = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        Assert.Equal(PayOsPaymentResolution.Confirmed, resolved.Resolution);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal("Paid", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime, order.PaidAt);
        Assert.Equal(now.UtcDateTime, order.CreatedOrderAt);

        var confirmation = Assert.Single(app.StockConfirmations);
        Assert.Equal($"order:{checkout.OrderId}", confirmation.SessionKey);
        Assert.Equal(checkout.OrderId, confirmation.OrderId);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Finalized, usage.Status);
        Assert.NotNull(usage.FinalizedAt);

        var createdEvent = Assert.Single(app.CreatedOrderEvents);
        Assert.Equal(checkout.OrderId, createdEvent.OrderId);
    }

    [Fact]
    public async Task Duplicate_PayOS_paid_result_does_not_repeat_created_order_effects()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-paid-duplicate-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(1));

        var duplicate = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        Assert.Equal(PayOsPaymentResolution.AlreadyResolved, duplicate.Resolution);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal(now.UtcDateTime, order.CreatedOrderAt);
        Assert.Equal(now.UtcDateTime, order.PaidAt);
        Assert.Single(app.StockConfirmations);
        Assert.Single(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_webhook_uses_payment_adapter_result_and_order_intake_lifecycle()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-webhook-adapter-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        app.SetNextWebhookResult(new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        const string rawPayload = """{"data":{"orderCode":123456789},"signature":"test"}""";
        var webhook = await app.Sender.Send(new PayOsWebhookCommand(rawPayload));

        Assert.True(webhook.Accepted);
        Assert.Equal([rawPayload], app.VerifiedWebhookPayloads);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal("Paid", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime, order.CreatedOrderAt);
        Assert.Single(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_failed_result_fails_order_attempt_and_releases_holds()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYFAIL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-failed-result-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYFAIL10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "payfail10"));

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

        var resolved = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Failed));

        Assert.Equal(PayOsPaymentResolution.Failed, resolved.Resolution);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime, order.CancelledAt);
        Assert.Null(order.CreatedOrderAt);
        Assert.Null(order.PaidAt);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);
        Assert.NotNull(usage.ReleasedAt);

        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_cancelled_result_cancels_order_attempt_and_releases_stock_hold()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-cancelled-result-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        var resolved = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Cancelled));

        Assert.Equal(PayOsPaymentResolution.Cancelled, resolved.Resolution);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime, order.CancelledAt);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Theory]
    [InlineData(PayOsPaymentResultStatus.Failed, PayOsPaymentResolution.Failed)]
    [InlineData(PayOsPaymentResultStatus.Cancelled, PayOsPaymentResolution.Cancelled)]
    public async Task PayOS_paid_result_after_failed_or_cancelled_attempt_does_not_revive_it(
        PayOsPaymentResultStatus terminalStatus,
        PayOsPaymentResolution terminalResolution)
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: $"payos-terminal-then-paid-{terminalStatus}",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        var terminal = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, terminalStatus));

        Assert.Equal(terminalResolution, terminal.Resolution);

        var latePaid = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        Assert.Equal(PayOsPaymentResolution.AlreadyResolved, latePaid.Resolution);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Null(order.CreatedOrderAt);
        Assert.Null(order.PaidAt);

        Assert.Single(app.StockReleases);
        Assert.Empty(app.StockConfirmations);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Customer_can_cancel_owned_uncreated_PayOS_order_attempt_through_order_intake()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYCANCEL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-customer-cancel-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYCANCEL10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paycancel10"));

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

        var cancelled = await app.OrderIntake.CancelOrderAttemptAsync(new CancelOrderAttempt(
            checkout.OrderId,
            RequestUserId: null,
            RequestEmail: "jane@example.com",
            Reason: "Changed mind"));

        Assert.Equal(checkout.OrderId, cancelled.OrderId);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Failed", cancelled.PaymentStatus);
        Assert.Equal(now.UtcDateTime, cancelled.CancelledAt);
        Assert.Equal("Changed mind", cancelled.CancellationReason);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime, order.CancelledAt);
        Assert.Equal("Changed mind", order.CancellationReason);
        Assert.Null(order.CreatedOrderAt);
        Assert.Null(order.PaidAt);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);
        Assert.NotNull(usage.ReleasedAt);

        Assert.Empty(app.StockConfirmations);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Broader_order_status_cancellation_rejects_uncreated_PayOS_order_attempt_without_releasing_voucher_hold()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "ADMINCANCEL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "admin-cannot-cancel-uncreated-payos-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("ADMINCANCEL10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "admincancel10"));

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

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new UpdateOrderStatusCommand(
                checkout.OrderId,
                OrderStatus.Cancelled,
                Reason: "Admin cancellation")));

        Assert.Contains("Order Intake", ex.Message);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CreatedOrderAt);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Held, usage.Status);

        Assert.Empty(app.StockReleases);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Customer_cannot_cancel_unowned_PayOS_order_attempt()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-unauthorized-cancel-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            app.Sender.Send(new CancelPendingPayOsOrderCommand(
                checkout.OrderId,
                RequestUserId: null,
                RequestEmail: "intruder@example.com",
                Reason: "Not mine")));

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancellationReason);

        Assert.Empty(app.StockReleases);
        Assert.Empty(app.StockConfirmations);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Customer_cannot_cancel_PayOS_attempt_after_it_becomes_created_order()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-created-order-cancel-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.OrderIntake.CancelOrderAttemptAsync(new CancelOrderAttempt(
                checkout.OrderId,
                RequestUserId: null,
                RequestEmail: "jane@example.com",
                Reason: "Changed mind")));

        Assert.Contains("Created Orders", ex.Message);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Confirmed", order.Status);
        Assert.Equal("Paid", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime, order.CreatedOrderAt);
        Assert.Equal(now.UtcDateTime, order.PaidAt);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancellationReason);

        Assert.Single(app.StockConfirmations);
        Assert.Empty(app.StockReleases);
        Assert.Single(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_cancel_after_settlement_grace_lazily_expires_instead_of_using_frontend_reason()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYCANCELGRACE10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-cancel-after-grace-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYCANCELGRACE10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paycancelgrace10"));

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

        app.TimeProvider.Advance(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));

        var cancelled = await app.Sender.Send(new CancelPendingPayOsOrderCommand(
            checkout.OrderId,
            RequestUserId: null,
            RequestEmail: "jane@example.com",
            Reason: "Frontend cancel redirect"));

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Failed", cancelled.PaymentStatus);
        Assert.Equal(now.UtcDateTime.AddMinutes(6).AddSeconds(1), cancelled.CancelledAt);
        Assert.Contains("Quá thời gian", cancelled.CancellationReason);
        Assert.DoesNotContain("Frontend", cancelled.CancellationReason);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);

        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_cancel_during_settlement_grace_does_not_release_holds_without_verified_result()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYCANCELWAIT10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-cancel-during-grace-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYCANCELWAIT10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paycancelwait10"));

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

        app.TimeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CancelPendingPayOsOrderCommand(
                checkout.OrderId,
                RequestUserId: null,
                RequestEmail: "jane@example.com",
                Reason: "Frontend cancel redirect")));

        Assert.Contains("settlement grace", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(app.StockReleases);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Held, usage.Status);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancellationReason);
    }

    [Fact]
    public async Task PayOS_webhook_with_unknown_order_code_is_acknowledged_without_local_state_changes()
    {
        await using var app = new CommerceTestApp();
        app.SetNextWebhookResult(new PayOsPaymentResult(987654321, PayOsPaymentResultStatus.Paid));

        const string rawPayload = """{"data":{"orderCode":987654321},"signature":"test"}""";
        var webhook = await app.Sender.Send(new PayOsWebhookCommand(rawPayload));

        Assert.True(webhook.Accepted);
        Assert.Equal([rawPayload], app.VerifiedWebhookPayloads);
        Assert.Empty(app.StockConfirmations);
        Assert.Empty(app.StockReleases);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_late_webhook_with_pending_provider_result_expires_touched_attempt()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-late-webhook-pending-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));
        app.SetNextWebhookResult(new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Pending));

        const string rawPayload = """{"data":{"orderCode":123456789,"status":"PENDING"},"signature":"test"}""";
        var webhook = await app.Sender.Send(new PayOsWebhookCommand(rawPayload));

        Assert.True(webhook.Accepted);
        Assert.Equal([rawPayload], app.VerifiedWebhookPayloads);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime.AddMinutes(6).AddSeconds(1), order.CancelledAt);
        Assert.Contains("Quá thời gian", order.CancellationReason);
        Assert.Null(order.CreatedOrderAt);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_pending_and_processing_results_wait_until_settlement_grace_passes_then_expire()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-pending-processing-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30)));

        var pending = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Pending));

        Assert.Equal(PayOsPaymentResolution.Waiting, pending.Resolution);

        var waitingOrder = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Pending", waitingOrder.Status);
        Assert.Equal("Pending", waitingOrder.PaymentStatus);
        Assert.Empty(app.StockReleases);

        app.TimeProvider.Advance(TimeSpan.FromSeconds(31));

        var processing = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Processing));

        Assert.Equal(PayOsPaymentResolution.Expired, processing.Resolution);

        var expiredOrder = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", expiredOrder.Status);
        Assert.Equal("Failed", expiredOrder.PaymentStatus);
        Assert.Equal(now.UtcDateTime.AddMinutes(6).AddSeconds(1), expiredOrder.CancelledAt);
        Assert.Null(expiredOrder.CreatedOrderAt);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Specific_order_read_lazily_expires_PayOS_attempt_after_settlement_grace()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYREAD10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-lazy-read-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYREAD10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "payread10"));

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

        app.TimeProvider.Advance(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));

        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime.AddMinutes(6).AddSeconds(1), order.CancelledAt);
        Assert.Contains("Quá thời gian", order.CancellationReason);
        Assert.Null(order.CreatedOrderAt);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);
        Assert.NotNull(usage.ReleasedAt);

        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Broad_order_listing_does_not_sweep_expired_PayOS_attempts()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYLIST10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-listing-no-sweep-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYLIST10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paylist10"));

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

        app.TimeProvider.Advance(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));

        var orders = await app.Sender.Send(new GetOrdersQuery(PageSize: 100));

        var summary = Assert.Single(orders.Orders);
        Assert.Equal(checkout.OrderId, summary.Id);
        Assert.Equal("Pending", summary.Status);
        Assert.Equal("Pending", summary.PaymentStatus);
        Assert.Empty(app.StockReleases);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Held, usage.Status);
    }

    [Fact]
    public async Task PayOS_paid_result_after_settlement_grace_is_anomaly_not_created_order()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-late-paid-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));

        var resolved = await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        Assert.Equal(PayOsPaymentResolution.LatePaidAfterGrace, resolved.Resolution);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime.AddMinutes(6).AddSeconds(1), order.CancelledAt);
        Assert.Contains("late", order.CancellationReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(order.PaidAt);
        Assert.Null(order.CreatedOrderAt);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{checkout.OrderId}", release.SessionKey);
        Assert.Empty(app.StockConfirmations);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_late_paid_after_grace_webhook_surfaces_operational_anomaly()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-late-paid-webhook-session",
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
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel")));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));
        app.SetNextWebhookResult(new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        const string rawPayload = """{"data":{"orderCode":123456789},"signature":"test"}""";
        var webhook = await app.Sender.Send(new PayOsWebhookCommand(rawPayload));

        Assert.True(webhook.Accepted);
        Assert.Equal(checkout.OrderId, webhook.OrderId);
        Assert.Equal(PayOsPaymentResolution.LatePaidAfterGrace, webhook.Resolution);
        Assert.Equal([rawPayload], app.VerifiedWebhookPayloads);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Null(order.CreatedOrderAt);
    }

    [Fact]
    public async Task PayOS_start_creates_uncreated_order_attempt_with_live_payment_window_holds_and_link_identity()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(
            now,
            new OrderIntakePaymentWindowPolicy(
                PaymentWindow: TimeSpan.FromMinutes(5),
                PaymentSettlementGrace: TimeSpan.FromMinutes(1)));

        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYSTART10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-start-window-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYSTART10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paystart10"));

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

        Assert.Equal("https://pay.example/checkout", checkout.PaymentUrl);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal(addResult.CartId, order.SourceCartId);
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Equal("PayOS", order.PaymentMethod);
        Assert.Null(order.CreatedOrderAt);

        var expectedWindowEnd = now.UtcDateTime.AddMinutes(5);
        var expectedGraceEnd = now.UtcDateTime.AddMinutes(6);

        var paymentState = await app.GetOrderPaymentStateAsync(checkout.OrderId);
        Assert.Equal(123456789, paymentState.PayOsOrderCode);
        Assert.Equal("test-payment-link-id", paymentState.PaymentLinkId);
        Assert.Equal("https://pay.example/checkout", paymentState.PaymentLinkUrl);
        Assert.Equal(expectedWindowEnd, paymentState.PaymentWindowExpiresAt);
        Assert.Equal(expectedGraceEnd, paymentState.PaymentSettlementGraceEndsAt);

        var paymentAttempt = Assert.Single(app.PaymentLinkAttempts);
        Assert.Equal(expectedWindowEnd, paymentAttempt.ExpiresAt.UtcDateTime);

        var stockHold = Assert.Single(app.StockReservations);
        Assert.Equal($"order:{checkout.OrderId}", stockHold.SessionKey);
        Assert.Equal(TimeSpan.FromMinutes(6), stockHold.Ttl);
        Assert.Empty(app.StockConfirmations);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Held, usage.Status);

        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_start_failure_preserves_active_cart_and_releases_holds()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYFAIL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-start-failure-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYFAIL10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "payfail10"));
        app.OnCreatePaymentLink(_ => throw new InvalidOperationException("PayOS unavailable"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
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
                CancelUrl: "https://shop.example/checkout/cancel"))));

        Assert.Contains("PayOS unavailable", ex.Message);

        var stockHold = Assert.Single(app.StockReservations);
        var release = Assert.Single(app.StockReleases);
        Assert.Equal(stockHold.SessionKey, release.SessionKey);

        var orderId = Guid.Parse(stockHold.SessionKey["order:".Length..]);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            app.Sender.Send(new GetOrderQuery(Id: orderId)));

        var cart = await app.GetCartStateAsync(addResult.CartId);
        Assert.Equal(CartStatus.Active, cart.Status);
        Assert.Equal(1, cart.ItemCount);
        Assert.Equal("PAYFAIL10", cart.AppliedVoucherCode);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);
    }

    [Fact]
    public async Task PayOS_start_failure_after_provider_link_cancels_link_restores_cart_and_releases_holds()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYLINKFAIL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-link-created-then-fails-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYLINKFAIL10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paylinkfail10"));

        app.FailOrderSavesWhen(orderDb => orderDb.ChangeTracker
            .Entries<CustomerOrder>()
            .Any(entry => entry.Entity.SourceCartId == addResult.CartId
                && entry.Entity.PayOsOrderCode.HasValue
                && entry.State == EntityState.Modified));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
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
                CancelUrl: "https://shop.example/checkout/cancel"))));

        Assert.Contains("order save failed", ex.Message);

        var cancellation = Assert.Single(app.PaymentLinkCancellations);
        Assert.Equal(123456789, cancellation.OrderCode);

        var stockHold = Assert.Single(app.StockReservations);
        var release = Assert.Single(app.StockReleases);
        Assert.Equal(stockHold.SessionKey, release.SessionKey);

        var orderId = Guid.Parse(stockHold.SessionKey["order:".Length..]);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            app.Sender.Send(new GetOrderQuery(Id: orderId)));

        var cart = await app.GetCartStateAsync(addResult.CartId);
        Assert.Equal(CartStatus.Active, cart.Status);
        Assert.Equal(1, cart.ItemCount);
        Assert.Equal("PAYLINKFAIL10", cart.AppliedVoucherCode);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);
    }

    [Theory]
    [InlineData(PaymentMethod.PayOS)]
    [InlineData(PaymentMethod.BankTransfer)]
    public async Task Order_intake_early_order_save_failure_releases_stock_and_preserves_active_cart(
        PaymentMethod paymentMethod)
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        var voucherCode = $"EARLYSAVE{paymentMethod}";

        await app.SeedVoucherAsync(
            code: voucherCode,
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: $"early-order-save-failure-{paymentMethod}",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync(voucherCode);
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, voucherCode));

        app.FailOrderSavesWhen(orderDb => orderDb.ChangeTracker
            .Entries<CustomerOrder>()
            .Any(entry => entry.Entity.SourceCartId == addResult.CartId
                && entry.State == EntityState.Added));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
                CartId: addResult.CartId,
                CustomerId: null,
                CustomerName: "Jane Shopper",
                CustomerEmail: "jane@example.com",
                CustomerPhone: "0900000000",
                ShippingAddress: "1 Test Street",
                ShippingCity: "Ha Noi",
                ShippingWard: "Ward 1",
                ShippingNote: null,
                PaymentMethod: paymentMethod,
                ReturnUrl: "https://shop.example/checkout/return",
                CancelUrl: "https://shop.example/checkout/cancel"))));

        Assert.Contains("order save failed", ex.Message);

        var stockHold = Assert.Single(app.StockReservations);
        var release = Assert.Single(app.StockReleases);
        Assert.Equal(stockHold.SessionKey, release.SessionKey);

        var orderId = Guid.Parse(stockHold.SessionKey["order:".Length..]);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            app.SendInNewScopeAsync(new GetOrderQuery(Id: orderId)));

        var cart = await app.GetCartStateAsync(addResult.CartId);
        Assert.Equal(CartStatus.Active, cart.Status);
        Assert.Equal(1, cart.ItemCount);
        Assert.Equal(voucherCode.ToUpperInvariant(), cart.AppliedVoucherCode);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        Assert.Empty(audit.Usages);
        Assert.Empty(app.PaymentLinkAttempts);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_start_reuses_existing_attempt_and_link_while_payment_window_is_live()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-live-retry-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var request = new CheckoutRequest(
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
            CancelUrl: "https://shop.example/checkout/cancel");

        var first = await app.Sender.Send(new CheckoutCommand(request));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(4));

        var retry = await app.Sender.Send(new CheckoutCommand(request));

        Assert.Equal(first.OrderId, retry.OrderId);
        Assert.Equal(first.OrderNumber, retry.OrderNumber);
        Assert.Equal(first.Total, retry.Total);
        Assert.Equal(first.Status, retry.Status);
        Assert.Equal(first.PaymentUrl, retry.PaymentUrl);

        Assert.Single(app.PaymentLinkAttempts);
        Assert.Single(app.StockReservations);
        Assert.Empty(app.StockConfirmations);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_start_after_payment_window_is_not_resurrected_as_a_checkout_retry()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-expired-retry-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var request = new CheckoutRequest(
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
            CancelUrl: "https://shop.example/checkout/cancel");

        var first = await app.Sender.Send(new CheckoutCommand(request));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(request)));

        Assert.Contains("Payment Window", ex.Message);

        var paymentState = await app.GetOrderPaymentStateAsync(first.OrderId);
        Assert.Equal("https://pay.example/checkout", paymentState.PaymentLinkUrl);
        Assert.Single(app.PaymentLinkAttempts);
        Assert.Single(app.StockReservations);
        Assert.Empty(app.StockConfirmations);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task PayOS_order_attempt_retry_after_settlement_grace_expires_attempt_without_resurrecting_link()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYRETRYGRACE10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-retry-after-grace-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYRETRYGRACE10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "payretrygrace10"));

        var request = new CheckoutRequest(
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
            CancelUrl: "https://shop.example/checkout/cancel");

        var first = await app.Sender.Send(new CheckoutCommand(request));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(6).Add(TimeSpan.FromSeconds(1)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(request)));

        Assert.Contains("fresh Cart Quote", ex.Message);
        Assert.Single(app.PaymentLinkAttempts);

        var release = Assert.Single(app.StockReleases);
        Assert.Equal($"order:{first.OrderId}", release.SessionKey);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);

        var order = await app.Sender.Send(new GetOrderQuery(Id: first.OrderId));
        Assert.Equal("Cancelled", order.Status);
        Assert.Equal("Failed", order.PaymentStatus);
        Assert.Equal(now.UtcDateTime.AddMinutes(6).AddSeconds(1), order.CancelledAt);
        Assert.Contains("Quá thời gian", order.CancellationReason);
        Assert.Null(order.CreatedOrderAt);
    }

    [Theory]
    [InlineData(PaymentMethod.CashOnDelivery)]
    [InlineData(PaymentMethod.BankTransfer)]
    public async Task Manual_payment_checkout_retry_returns_existing_created_order_attempt(PaymentMethod paymentMethod)
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: $"manual-idempotent-retry-{paymentMethod}",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var request = new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerId: null,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: paymentMethod);

        var first = await app.Sender.Send(new CheckoutCommand(request));
        var retry = await app.Sender.Send(new CheckoutCommand(request));

        Assert.Equal(first.OrderId, retry.OrderId);
        Assert.Equal(first.OrderNumber, retry.OrderNumber);
        Assert.Equal(first.Total, retry.Total);
        Assert.Equal(first.Status, retry.Status);
        Assert.Null(retry.PaymentUrl);

        Assert.Single(app.StockReservations);
        Assert.Single(app.StockConfirmations);
        Assert.Single(app.CreatedOrderEvents);
        Assert.Empty(app.PaymentLinkAttempts);
    }

    [Fact]
    public async Task BankTransfer_order_intake_becomes_created_order_and_commits_stock_immediately()
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
    public async Task Manual_payment_order_intake_failure_after_consuming_cart_restores_cart_and_releases_holds()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "MANUALFAIL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "manual-created-order-failure-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("MANUALFAIL10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "manualfail10"));
        app.OnConfirmStock(_ => throw new InvalidOperationException("stock confirmation failed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
                CartId: addResult.CartId,
                CustomerId: null,
                CustomerName: "Jane Shopper",
                CustomerEmail: "jane@example.com",
                CustomerPhone: "0900000000",
                ShippingAddress: "1 Test Street",
                ShippingCity: "Ha Noi",
                ShippingWard: "Ward 1",
                ShippingNote: null,
                PaymentMethod: PaymentMethod.BankTransfer))));

        Assert.Contains("stock confirmation failed", ex.Message);

        var stockHold = Assert.Single(app.StockReservations);
        var release = Assert.Single(app.StockReleases);
        Assert.Equal(stockHold.SessionKey, release.SessionKey);

        var orderId = Guid.Parse(stockHold.SessionKey["order:".Length..]);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            app.Sender.Send(new GetOrderQuery(Id: orderId)));

        var cart = await app.GetCartStateAsync(addResult.CartId);
        Assert.Equal(CartStatus.Active, cart.Status);
        Assert.Equal(1, cart.ItemCount);
        Assert.Equal("MANUALFAIL10", cart.AppliedVoucherCode);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Manual_payment_order_intake_finalizes_voucher_usage_for_the_created_order()
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
    public async Task PayOS_paid_result_final_save_failure_restores_confirmed_stock_and_releases_voucher()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYSAVEFAIL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-paid-save-failure-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var voucherId = await app.GetVoucherIdAsync("PAYSAVEFAIL10");
        await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "paysavefail10"));

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

        app.FailOrderSavesWhen(orderDb => orderDb.ChangeTracker
            .Entries<CustomerOrder>()
            .Any(entry => entry.Entity.Id == checkout.OrderId
                && entry.Entity.CreatedOrderAt is not null
                && entry.Entity.PaymentStatus == PaymentStatus.Paid
                && entry.State == EntityState.Modified));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.OrderIntake.ResolvePayOsPaymentResultAsync(
                new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid)));

        Assert.Contains("order save failed", ex.Message);

        var restore = Assert.Single(app.StockRestores);
        Assert.Equal($"order:{checkout.OrderId}", restore.SessionKey);
        Assert.Equal(checkout.OrderId, restore.OrderId);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);

        var order = await app.GetOrderStateFreshAsync(checkout.OrderId);
        Assert.Equal("Pending", order.Status);
        Assert.Equal("Pending", order.PaymentStatus);
        Assert.Null(order.CreatedOrderAt);
        Assert.Empty(app.CreatedOrderEvents);
    }

    [Fact]
    public async Task Concurrent_voucher_holds_do_not_oversell_limited_use_capacity()
    {
        await using var app = new CommerceTestApp();

        await app.SeedVoucherAsync(
            code: "CONCURRENT1",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var voucherId = await app.GetVoucherIdAsync("CONCURRENT1");
        var firstOrderId = Guid.NewGuid();
        var secondOrderId = Guid.NewGuid();

        var attempts = new[]
        {
            app.SendInNewScopeAsync(new HoldVoucherUsageCommand(voucherId, null, firstOrderId, 10m)),
            app.SendInNewScopeAsync(new HoldVoucherUsageCommand(voucherId, null, secondOrderId, 10m))
        };

        var outcomes = await Task.WhenAll(attempts.Select(async attempt =>
        {
            try
            {
                await attempt;
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("hết lượt"))
            {
                return false;
            }
        }));

        Assert.Single(outcomes, success => success);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var heldUsage = Assert.Single(audit.Usages, u => u.Status == VoucherUsageStatus.Held);
        Assert.Contains(heldUsage.OrderId, new[] { firstOrderId, secondOrderId });
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
