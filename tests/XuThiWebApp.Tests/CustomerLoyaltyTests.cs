using Cart.ShoppingCarts.Features.AddItemIntoCart;
using Contracts;
using Customer.Customers.Features.AddCustomerOrder;
using Customer.Customers.Features.GetCustomer;
using Customer.Customers.Features.GetCustomerLoyaltyHistory;
using Customer.Customers.Models;
using Customer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Order.Data;
using Order.Orders.Features.Checkout;
using Order.Orders.Features.UpdateOrderStatus;
using Order.Orders.Models;
using Order.Orders.OrderIntake;

namespace XuThiWebApp.Tests;

public sealed class CustomerLoyaltyTests
{
    [Fact]
    public async Task Old_add_customer_order_command_is_retired_for_automatic_loyalty_awards()
    {
        await using var app = new CommerceTestApp();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new AddCustomerOrderCommand(
                CustomerId: app.DefaultCustomerId,
                OrderTotal: 100_000m,
                PointsEarned: 10,
                OrderId: Guid.NewGuid())));

        Assert.Contains("retired", ex.Message);

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(0m, customer.TotalLoyaltySpend);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(0, customer.TotalOrders);
        Assert.Empty(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
    }

    [Fact]
    public async Task Delivered_order_awards_customer_loyalty_and_records_snapshot()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100_000m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "delivered-loyalty-award-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.CashOnDelivery), app.DefaultExternalUserId));

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Confirmed));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Processing));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Shipped));

        app.TimeProvider.Advance(TimeSpan.FromHours(3));
        var deliveredAt = app.TimeProvider.GetUtcNow().UtcDateTime;

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Delivered));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(200_000m, customer.TotalLoyaltySpend);
        Assert.Equal(20, customer.LoyaltyPoints);
        Assert.Equal(1, customer.TotalOrders);
        Assert.Equal(deliveredAt, customer.LastOrderAt);

        var history = await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId);
        var award = Assert.Single(history);
        Assert.Equal(LoyaltyTransactionType.Awarded, award.Type);
        Assert.Equal(20, award.PointsDelta);
        Assert.Equal(20, award.PointsBalanceAfter);
        Assert.Equal(200_000m, award.LoyaltySpendDelta);
        Assert.Equal(200_000m, award.TotalLoyaltySpendAfter);
        Assert.Equal(1, award.TotalOrdersAfter);
        Assert.Equal(CustomerTier.Standard, award.TierAfter);
        Assert.Equal(deliveredAt, award.OccurredAt);
        Assert.Equal(checkout.OrderId, award.RelatedOrderId);
        Assert.Equal(checkout.OrderNumber, award.OrderNumber);
    }

    [Fact]
    public async Task Delivered_outcome_uses_discounted_subtotal_as_loyalty_spend_and_excludes_shipping()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: "XT-20260624-DISCOUNT-SHIPPING",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: now.UtcDateTime,
            Subtotal: 25_000m,
            DiscountAmount: 15_001m,
            ShippingFee: 40_000m,
            Total: 49_999m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(9_999m, customer.TotalLoyaltySpend);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(1, customer.TotalOrders);
        Assert.Equal(now.UtcDateTime, customer.LastOrderAt);

        var award = Assert.Single(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
        Assert.Equal(LoyaltyTransactionType.Awarded, award.Type);
        Assert.Equal(0, award.PointsDelta);
        Assert.Equal(9_999m, award.LoyaltySpendDelta);
        Assert.Equal(9_999m, award.TotalLoyaltySpendAfter);
        Assert.Equal(1, award.TotalOrdersAfter);
        Assert.Equal(now.UtcDateTime, award.OccurredAt);
        Assert.Equal(orderId, award.RelatedOrderId);
        Assert.Equal("XT-20260624-DISCOUNT-SHIPPING", award.OrderNumber);
    }

    [Fact]
    public async Task Delivered_outcome_rejects_order_linked_award_when_loyalty_spend_is_not_positive()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
                CustomerId: app.DefaultCustomerId,
                OrderId: Guid.NewGuid(),
                OrderNumber: "XT-20260624-ZERO-SPEND",
                Outcome: CustomerOrderOutcome.Delivered,
                OccurredAt: now.UtcDateTime,
                Subtotal: 50_000m,
                DiscountAmount: 50_000m,
                ShippingFee: 30_000m,
                Total: 30_000m)));

        Assert.Contains("Loyalty Spend must be positive", ex.Message);
        Assert.Empty(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
    }

    [Fact]
    public async Task PayOS_payment_waits_until_delivered_outcome_to_award_customer_loyalty()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100_000m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-loyalty-waits-for-delivery-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.PayOS,
            ReturnUrl: "https://shop.example/checkout/return",
            CancelUrl: "https://shop.example/checkout/cancel"), app.DefaultExternalUserId));

        await app.OrderIntake.ResolvePayOsPaymentResultAsync(
            new PayOsPaymentResult(123456789, PayOsPaymentResultStatus.Paid));

        var paidCustomer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(paidCustomer);
        Assert.Equal(0m, paidCustomer.TotalLoyaltySpend);
        Assert.Equal(0, paidCustomer.LoyaltyPoints);
        Assert.Equal(0, paidCustomer.TotalOrders);
        Assert.Empty(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Processing));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Shipped));

        app.TimeProvider.Advance(TimeSpan.FromDays(2));
        var deliveredAt = app.TimeProvider.GetUtcNow().UtcDateTime;
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Delivered));

        var deliveredCustomer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(deliveredCustomer);
        Assert.Equal(100_000m, deliveredCustomer.TotalLoyaltySpend);
        Assert.Equal(10, deliveredCustomer.LoyaltyPoints);
        Assert.Equal(1, deliveredCustomer.TotalOrders);
        Assert.Equal(deliveredAt, deliveredCustomer.LastOrderAt);

        var award = Assert.Single(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
        Assert.Equal(LoyaltyTransactionType.Awarded, award.Type);
        Assert.Equal(deliveredAt, award.OccurredAt);
        Assert.Equal(checkout.OrderId, award.RelatedOrderId);
    }

    [Fact]
    public async Task Delivered_outcome_recomputes_customer_tier_from_total_loyalty_spend()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: "XT-20260624-SILVER",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: now.UtcDateTime,
            Subtotal: 1_000_000m,
            DiscountAmount: 0m,
            ShippingFee: 0m,
            Total: 1_000_000m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(1_000_000m, customer.TotalLoyaltySpend);
        Assert.Equal(CustomerTier.Silver, customer.Tier);

        var award = Assert.Single(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
        Assert.Equal(CustomerTier.Silver, award.TierAfter);
    }

    [Fact]
    public async Task Duplicate_matching_delivered_outcome_does_not_award_customer_loyalty_twice()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100_000m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "duplicate-delivered-loyalty-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.CashOnDelivery), app.DefaultExternalUserId));

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Confirmed));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Processing));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Shipped));

        app.TimeProvider.Advance(TimeSpan.FromHours(3));
        var deliveredAt = app.TimeProvider.GetUtcNow().UtcDateTime;

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Delivered));
        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: checkout.OrderId,
            OrderNumber: checkout.OrderNumber,
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: deliveredAt,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 0m,
            Total: 200_000m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(200_000m, customer.TotalLoyaltySpend);
        Assert.Equal(20, customer.LoyaltyPoints);
        Assert.Equal(1, customer.TotalOrders);

        var history = await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId);
        Assert.Single(history);
    }

    [Fact]
    public async Task Duplicate_key_race_for_matching_delivered_award_is_idempotent_success()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();

        app.RaceNextCustomerLoyaltyHistorySave();

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: "XT-20260624-AWARD-RACE",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: now.UtcDateTime,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 230_000m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(200_000m, customer.TotalLoyaltySpend);
        Assert.Equal(20, customer.LoyaltyPoints);
        Assert.Equal(1, customer.TotalOrders);

        var award = Assert.Single(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
        Assert.Equal(LoyaltyTransactionType.Awarded, award.Type);
        Assert.Equal(orderId, award.RelatedOrderId);
        Assert.Equal("XT-20260624-AWARD-RACE", award.OrderNumber);
    }

    [Fact]
    public async Task Duplicate_key_race_for_matching_cancelled_reversal_is_idempotent_success()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();
        const string orderNumber = "XT-20260624-REVERSAL-RACE";

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: orderNumber,
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: now.UtcDateTime,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 230_000m));

        app.RaceNextCustomerLoyaltyHistorySave();

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: orderNumber,
            Outcome: CustomerOrderOutcome.Cancelled,
            OccurredAt: now.AddDays(1).UtcDateTime,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 230_000m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(0m, customer.TotalLoyaltySpend);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(0, customer.TotalOrders);

        var history = await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId);
        Assert.Equal(2, history.Count);
        Assert.Equal(LoyaltyTransactionType.Reversed, history[0].Type);
        Assert.Equal(orderId, history[0].RelatedOrderId);
        Assert.Equal(orderNumber, history[0].OrderNumber);
    }

    [Fact]
    public async Task Database_error_while_recording_delivered_award_bubbles()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);

        app.FailNextCustomerLoyaltyHistorySave(
            new DbUpdateException("customer loyalty database write failed"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
                CustomerId: app.DefaultCustomerId,
                OrderId: Guid.NewGuid(),
                OrderNumber: "XT-20260624-DB-ERROR",
                Outcome: CustomerOrderOutcome.Delivered,
                OccurredAt: now.UtcDateTime,
                Subtotal: 200_000m,
                DiscountAmount: 0m,
                ShippingFee: 30_000m,
                Total: 230_000m)));

        Assert.Contains("customer loyalty database write failed", ex.Message);
        Assert.Empty(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
    }

    [Fact]
    public async Task Conflicting_duplicate_delivered_outcome_fails_loudly()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100_000m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "conflicting-duplicate-delivered-loyalty-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.CashOnDelivery), app.DefaultExternalUserId));

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Confirmed));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Processing));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Shipped));

        app.TimeProvider.Advance(TimeSpan.FromHours(3));
        var deliveredAt = app.TimeProvider.GetUtcNow().UtcDateTime;

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Delivered));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
                CustomerId: app.DefaultCustomerId,
                OrderId: checkout.OrderId,
                OrderNumber: checkout.OrderNumber,
                Outcome: CustomerOrderOutcome.Delivered,
                OccurredAt: deliveredAt,
                Subtotal: 210_000m,
                DiscountAmount: 0m,
                ShippingFee: 0m,
                Total: 210_000m)));

        Assert.Contains("Conflicting duplicate", ex.Message);
    }

    [Fact]
    public async Task Conflicting_duplicate_cancelled_reversal_outcome_fails_loudly()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();
        const string orderNumber = "XT-20260624-CONFLICTING-REVERSAL";

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: orderNumber,
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: now.UtcDateTime,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 230_000m));

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: orderNumber,
            Outcome: CustomerOrderOutcome.Cancelled,
            OccurredAt: now.AddDays(1).UtcDateTime,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 230_000m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
                CustomerId: app.DefaultCustomerId,
                OrderId: orderId,
                OrderNumber: orderNumber,
                Outcome: CustomerOrderOutcome.Cancelled,
                OccurredAt: now.AddDays(1).UtcDateTime,
                Subtotal: 210_000m,
                DiscountAmount: 0m,
                ShippingFee: 30_000m,
                Total: 240_000m)));

        Assert.Contains("Conflicting duplicate", ex.Message);
        var history = await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId);
        Assert.Equal(2, history.Count);
    }

    [Theory]
    [InlineData(CustomerOrderOutcome.Returned, "XT-20260624-NO-AWARD-RETURNED")]
    [InlineData(CustomerOrderOutcome.Cancelled, "XT-20260624-NO-AWARD-CANCELLED")]
    public async Task Returned_or_cancelled_outcome_without_prior_award_is_noop_and_logs_info(
        CustomerOrderOutcome outcome,
        string orderNumber)
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: orderNumber,
            Outcome: outcome,
            OccurredAt: now.UtcDateTime,
            Subtotal: 100_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 130_000m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(0m, customer.TotalLoyaltySpend);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(0, customer.TotalOrders);
        Assert.Null(customer.LastOrderAt);
        Assert.Empty(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
        Assert.Contains(app.LogEntries, entry =>
            entry.Level == LogLevel.Information
            && entry.CategoryName.Contains("CustomerLoyaltyOutcomeRecorder")
            && entry.Message.Contains("no prior Loyalty Award")
            && entry.Message.Contains(orderId.ToString()));
    }

    [Theory]
    [InlineData(CustomerOrderOutcome.Returned)]
    [InlineData(CustomerOrderOutcome.Cancelled)]
    public async Task Returned_or_cancelled_outcome_without_customer_fails_even_when_no_prior_award(
        CustomerOrderOutcome outcome)
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var missingCustomerId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
                CustomerId: missingCustomerId,
                OrderId: Guid.NewGuid(),
                OrderNumber: "XT-20260624-MISSING-CUSTOMER-REVERSAL",
                Outcome: outcome,
                OccurredAt: now.UtcDateTime,
                Subtotal: 100_000m,
                DiscountAmount: 0m,
                ShippingFee: 30_000m,
                Total: 130_000m)));

        Assert.Contains("was not found for Customer Loyalty", ex.Message);
        Assert.DoesNotContain(app.LogEntries, entry =>
            entry.CategoryName.Contains("CustomerLoyaltyOutcomeRecorder")
            && entry.Message.Contains("no prior Loyalty Award"));
    }

    [Fact]
    public async Task Cancelled_order_status_without_prior_award_flows_through_customer_loyalty_noop()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100_000m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "cancelled-status-loyalty-noop-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.BankTransfer), app.DefaultExternalUserId));

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Confirmed));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(30));
        var cancelledAt = app.TimeProvider.GetUtcNow().UtcDateTime;
        await app.Sender.Send(new UpdateOrderStatusCommand(
            checkout.OrderId,
            OrderStatus.Cancelled,
            Reason: "Customer requested cancellation"));

        var outcome = Assert.Single(app.CustomerOrderOutcomeEvents);
        Assert.Equal(CustomerOrderOutcome.Cancelled, outcome.Outcome);
        Assert.Equal(cancelledAt, outcome.OccurredAt);
        Assert.Equal(checkout.OrderId, outcome.OrderId);

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(0m, customer.TotalLoyaltySpend);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(0, customer.TotalOrders);
        Assert.Empty(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
        Assert.Contains(app.LogEntries, entry =>
            entry.Level == LogLevel.Information
            && entry.CategoryName.Contains("CustomerLoyaltyOutcomeRecorder")
            && entry.Message.Contains("no prior Loyalty Award")
            && entry.Message.Contains(checkout.OrderId.ToString()));
    }

    [Fact]
    public async Task Cancelled_outcome_reverses_original_award_facts_not_cancelled_shipping_or_total()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: "XT-20260624-CANCEL-REVERSAL",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: now.UtcDateTime,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 230_000m));

        var cancelledAt = now.AddDays(1).UtcDateTime;
        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: orderId,
            OrderNumber: "XT-20260624-CANCEL-REVERSAL",
            Outcome: CustomerOrderOutcome.Cancelled,
            OccurredAt: cancelledAt,
            Subtotal: 230_000m,
            DiscountAmount: 30_000m,
            ShippingFee: 0m,
            Total: 200_000m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(0m, customer.TotalLoyaltySpend);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(0, customer.TotalOrders);

        var history = await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId);
        Assert.Equal(2, history.Count);
        var reversal = history[0];
        Assert.Equal(LoyaltyTransactionType.Reversed, reversal.Type);
        Assert.Equal(-20, reversal.PointsDelta);
        Assert.Equal(-200_000m, reversal.LoyaltySpendDelta);
        Assert.Equal(0m, reversal.TotalLoyaltySpendAfter);
        Assert.Equal(0, reversal.TotalOrdersAfter);
        Assert.Equal(cancelledAt, reversal.OccurredAt);
        Assert.Equal(orderId, reversal.RelatedOrderId);
    }

    [Fact]
    public async Task Reversal_against_legacy_award_with_unknown_loyalty_spend_fails_loudly()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();

        await app.AddLoyaltyHistoryAsync(new LoyaltyHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = app.DefaultCustomerId,
            Type = LoyaltyTransactionType.Awarded,
            PointsDelta = 20,
            PointsBalanceAfter = 20,
            LoyaltySpendDelta = null,
            TotalLoyaltySpendAfter = 200_000m,
            TotalOrdersAfter = 1,
            TierAfter = CustomerTier.Standard,
            OccurredAt = now.UtcDateTime,
            Description = "Legacy award.",
            RelatedOrderId = orderId,
            OrderNumber = "XT-20260624-LEGACY"
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
                CustomerId: app.DefaultCustomerId,
                OrderId: orderId,
                OrderNumber: "XT-20260624-LEGACY",
                Outcome: CustomerOrderOutcome.Returned,
                OccurredAt: now.AddDays(1).UtcDateTime,
                Subtotal: 200_000m,
                DiscountAmount: 0m,
                ShippingFee: 0m,
                Total: 200_000m)));

        Assert.Contains("legacy Loyalty Award", ex.Message);
        Assert.Single(await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId));
    }

    [Fact]
    public async Task Reversal_that_would_make_customer_loyalty_spend_negative_fails_loudly()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var orderId = Guid.NewGuid();

        await app.AddLoyaltyHistoryAsync(new LoyaltyHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = app.DefaultCustomerId,
            Type = LoyaltyTransactionType.Awarded,
            PointsDelta = 20,
            PointsBalanceAfter = 20,
            LoyaltySpendDelta = 200_000m,
            TotalLoyaltySpendAfter = 200_000m,
            TotalOrdersAfter = 1,
            TierAfter = CustomerTier.Standard,
            OccurredAt = now.UtcDateTime,
            Description = "Award with drifted customer state.",
            RelatedOrderId = orderId,
            OrderNumber = "XT-20260624-NEGATIVE-GUARD"
        });
        await app.UpdateCustomerProfileAsync(app.DefaultCustomerId, customer =>
        {
            customer.TotalLoyaltySpend = 100_000m;
            customer.LoyaltyPoints = 20;
            customer.TotalOrders = 1;
            customer.LastOrderAt = now.UtcDateTime;
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
                CustomerId: app.DefaultCustomerId,
                OrderId: orderId,
                OrderNumber: "XT-20260624-NEGATIVE-GUARD",
                Outcome: CustomerOrderOutcome.Cancelled,
                OccurredAt: now.AddDays(1).UtcDateTime,
                Subtotal: 200_000m,
                DiscountAmount: 0m,
                ShippingFee: 0m,
                Total: 200_000m)));

        Assert.Contains("Spend cannot become negative", ex.Message);
        var history = await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId);
        Assert.Single(history);
        Assert.DoesNotContain(history, h => h.Type == LoyaltyTransactionType.Reversed);
    }

    [Fact]
    public async Task Reversal_recomputes_tier_and_last_order_at_from_remaining_awards()
    {
        var firstAwardAt = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(firstAwardAt);
        var firstOrderId = Guid.NewGuid();
        var secondOrderId = Guid.NewGuid();

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: firstOrderId,
            OrderNumber: "XT-20260624-REMAINING",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: firstAwardAt.UtcDateTime,
            Subtotal: 1_000_000m,
            DiscountAmount: 0m,
            ShippingFee: 0m,
            Total: 1_000_000m));

        var secondAwardAt = firstAwardAt.AddDays(1).UtcDateTime;
        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: secondOrderId,
            OrderNumber: "XT-20260625-REVERSED",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: secondAwardAt,
            Subtotal: 5_000_000m,
            DiscountAmount: 0m,
            ShippingFee: 0m,
            Total: 5_000_000m));

        var beforeReversal = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(beforeReversal);
        Assert.Equal(6_000_000m, beforeReversal.TotalLoyaltySpend);
        Assert.Equal(CustomerTier.Gold, beforeReversal.Tier);
        Assert.Equal(secondAwardAt, beforeReversal.LastOrderAt);

        var cancelledAt = firstAwardAt.AddDays(2).UtcDateTime;
        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: secondOrderId,
            OrderNumber: "XT-20260625-REVERSED",
            Outcome: CustomerOrderOutcome.Cancelled,
            OccurredAt: cancelledAt,
            Subtotal: 5_000_000m,
            DiscountAmount: 0m,
            ShippingFee: 0m,
            Total: 5_000_000m));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(1_000_000m, customer.TotalLoyaltySpend);
        Assert.Equal(CustomerTier.Silver, customer.Tier);
        Assert.Equal(firstAwardAt.UtcDateTime, customer.LastOrderAt);

        var reversal = (await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId))[0];
        Assert.Equal(LoyaltyTransactionType.Reversed, reversal.Type);
        Assert.Equal(CustomerTier.Silver, reversal.TierAfter);
        Assert.Equal(1_000_000m, reversal.TotalLoyaltySpendAfter);
        Assert.Equal(1, reversal.TotalOrdersAfter);
    }

    [Fact]
    public async Task Returned_order_reverses_prior_loyalty_award_and_records_snapshot()
    {
        var now = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(now);
        var item = await app.SeedCatalogItemAsync(price: 100_000m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "returned-loyalty-reversal-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.CashOnDelivery), app.DefaultExternalUserId));

        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Confirmed));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Processing));
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Shipped));

        app.TimeProvider.Advance(TimeSpan.FromHours(3));
        var deliveredAt = app.TimeProvider.GetUtcNow().UtcDateTime;
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Delivered));

        app.TimeProvider.Advance(TimeSpan.FromDays(2));
        var returnedAt = app.TimeProvider.GetUtcNow().UtcDateTime;
        await app.Sender.Send(new UpdateOrderStatusCommand(checkout.OrderId, OrderStatus.Returned));

        var customer = (await app.Sender.Send(new GetCustomerQuery(app.DefaultCustomerId))).Customer;
        Assert.NotNull(customer);
        Assert.Equal(0m, customer.TotalLoyaltySpend);
        Assert.Equal(0, customer.LoyaltyPoints);
        Assert.Equal(0, customer.TotalOrders);
        Assert.Null(customer.LastOrderAt);

        var history = await app.GetLoyaltyHistoryAsync(app.DefaultCustomerId);
        Assert.Equal(2, history.Count);
        var reversal = history[0];
        Assert.Equal(LoyaltyTransactionType.Reversed, reversal.Type);
        Assert.Equal(-20, reversal.PointsDelta);
        Assert.Equal(0, reversal.PointsBalanceAfter);
        Assert.Equal(-200_000m, reversal.LoyaltySpendDelta);
        Assert.Equal(0m, reversal.TotalLoyaltySpendAfter);
        Assert.Equal(0, reversal.TotalOrdersAfter);
        Assert.Equal(CustomerTier.Standard, reversal.TierAfter);
        Assert.Equal(returnedAt, reversal.OccurredAt);
        Assert.Equal(checkout.OrderId, reversal.RelatedOrderId);
        Assert.Equal(checkout.OrderNumber, reversal.OrderNumber);

        var award = history[1];
        Assert.Equal(LoyaltyTransactionType.Awarded, award.Type);
        Assert.Equal(deliveredAt, award.OccurredAt);

        var customerFacingHistory = await app.Sender.Send(
            new GetCustomerLoyaltyHistoryQuery(app.DefaultCustomerId));
        Assert.Equal(
            [LoyaltyTransactionType.Reversed, LoyaltyTransactionType.Awarded],
            customerFacingHistory.History.Select(h => h.Type).ToArray());
        Assert.Equal(
            [returnedAt, deliveredAt],
            customerFacingHistory.History.Select(h => h.OccurredAt).ToArray());
    }

    [Fact]
    public async Task Loyalty_history_uses_occurred_at_for_business_ordering_and_created_at_for_insertion_time()
    {
        var insertionTime = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);
        await using var app = new CommerceTestApp(insertionTime);
        var earlierOrderId = Guid.NewGuid();
        var laterOrderId = Guid.NewGuid();
        var earlierOccurredAt = insertionTime.AddDays(-2).UtcDateTime;
        var laterOccurredAt = insertionTime.AddDays(-1).UtcDateTime;

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: laterOrderId,
            OrderNumber: "XT-20260623-LATER",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: laterOccurredAt,
            Subtotal: 100_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 130_000m));

        app.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var secondInsertionTime = app.TimeProvider.GetUtcNow().UtcDateTime;

        await app.Publisher.Publish(new CustomerOrderOutcomeOccurred(
            CustomerId: app.DefaultCustomerId,
            OrderId: earlierOrderId,
            OrderNumber: "XT-20260622-EARLIER",
            Outcome: CustomerOrderOutcome.Delivered,
            OccurredAt: earlierOccurredAt,
            Subtotal: 200_000m,
            DiscountAmount: 0m,
            ShippingFee: 30_000m,
            Total: 230_000m));

        var customerFacingHistory = await app.Sender.Send(
            new GetCustomerLoyaltyHistoryQuery(app.DefaultCustomerId));

        Assert.Equal(
            ["XT-20260623-LATER", "XT-20260622-EARLIER"],
            customerFacingHistory.History.Select(h => h.OrderNumber!).ToArray());
        Assert.Equal(
            [laterOccurredAt, earlierOccurredAt],
            customerFacingHistory.History.Select(h => h.OccurredAt).ToArray());

        var earlierHistory = Assert.Single(
            customerFacingHistory.History,
            h => h.OrderNumber == "XT-20260622-EARLIER");
        Assert.Equal(secondInsertionTime, earlierHistory.CreatedAt);
        Assert.NotEqual(earlierHistory.OccurredAt, earlierHistory.CreatedAt);
    }

    [Fact]
    public void Customer_loyalty_model_has_non_negative_snapshot_constraints()
    {
        using var db = new CustomerDbContext(
            new DbContextOptionsBuilder<CustomerDbContext>()
                .UseInMemoryDatabase($"customer-model-{Guid.NewGuid():N}")
                .Options);

        var model = db.GetService<IDesignTimeModel>().Model;

        var customerConstraints = model
            .FindEntityType(typeof(CustomerProfile))!
            .GetCheckConstraints()
            .Select(c => c.Name)
            .ToHashSet();
        Assert.Contains("CK_Customers_LoyaltyPoints_NonNegative", customerConstraints);
        Assert.Contains("CK_Customers_TotalLoyaltySpend_NonNegative", customerConstraints);
        Assert.Contains("CK_Customers_TotalOrders_NonNegative", customerConstraints);

        var historyConstraints = model
            .FindEntityType(typeof(LoyaltyHistory))!
            .GetCheckConstraints()
            .Select(c => c.Name)
            .ToHashSet();
        Assert.Contains("CK_LoyaltyHistory_PointsBalanceAfter_NonNegative", historyConstraints);
        Assert.Contains("CK_LoyaltyHistory_TotalLoyaltySpendAfter_NonNegative", historyConstraints);
        Assert.Contains("CK_LoyaltyHistory_TotalOrdersAfter_NonNegative", historyConstraints);
    }

    [Fact]
    public void Order_model_requires_customer_reference_for_persisted_orders()
    {
        using var db = new OrderDbContext(
            new DbContextOptionsBuilder<OrderDbContext>()
                .UseInMemoryDatabase($"order-model-{Guid.NewGuid():N}")
                .Options);

        var customerId = db.Model
            .FindEntityType(typeof(CustomerOrder))!
            .FindProperty(nameof(CustomerOrder.CustomerId));

        Assert.NotNull(customerId);
        Assert.False(customerId.IsNullable);
    }
}
