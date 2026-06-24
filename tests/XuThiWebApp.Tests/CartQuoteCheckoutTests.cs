using Cart;
using Cart.Data;
using Cart.ShoppingCarts.Features.AddItemIntoCart;
using Cart.ShoppingCarts.Features.ApplyVoucher;
using Cart.ShoppingCarts.Features.ClearCart;
using Cart.ShoppingCarts.Features.GetCart;
using Cart.ShoppingCarts.Features.MergeCarts;
using Cart.ShoppingCarts.Features.RemoveFromCart;
using Cart.ShoppingCarts.Features.RemoveVoucher;
using Cart.ShoppingCarts.Features.SyncCartPrices;
using Cart.ShoppingCarts.Features.UpdateCartItem;
using Cart.ShoppingCarts.Models;
using Core.Caching;
using Core.Extensions;
using Customer;
using Customer.Data;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order;
using Order.Data;
using Order.Orders.Features.Checkout;
using Order.Orders.Features.GetOrder;
using Order.Orders.Features.GetOrders;
using Order.Orders.Models;
using Order.Orders.OrderIntake;
using Order.Orders.Services;
using PayOS.Models.Webhooks;
using ProductCatalog;
using ProductCatalog.Brands.Models;
using ProductCatalog.Categories.Models;
using ProductCatalog.Data;
using ProductCatalog.Products.Models;
using ProductCatalog.Products.Services;
using Promotion;
using Promotion.Data;
using Promotion.SaleCampaigns.Models;
using Promotion.Vouchers.Features.RedeemVoucher;
using Promotion.Vouchers.Features.ValidateVoucher;
using Promotion.Vouchers.Models;
using System.Text.Json;

namespace XuThiWebApp.Tests;

public sealed class CartQuoteCheckoutTests
{
    [Fact]
    public async Task Adding_item_quotes_active_cart_from_catalog_and_sale_facts()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        await app.SeedSaleAsync(item, salePrice: 80m, originalPrice: 100m);

        var result = await app.Sender.Send(new AddToCartCommand(
            SessionId: "quote-sale-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var cartItem = Assert.Single(result.Cart.Items);
        Assert.Equal(item.ProductId, cartItem.ProductId);
        Assert.Equal(item.VariantId, cartItem.VariantId);
        Assert.Equal("Test sneaker", cartItem.ProductName);
        Assert.Equal(item.Sku, cartItem.VariantSku);
        Assert.Equal(80m, cartItem.UnitPrice);
        Assert.Equal(100m, cartItem.CompareAtPrice);
        Assert.Equal(160m, cartItem.TotalPrice);
        Assert.Equal(5, cartItem.AvailableStock);
        Assert.True(cartItem.IsInStock);
        Assert.True(cartItem.IsOnSale);
        Assert.Equal(160m, result.Cart.Subtotal);
        Assert.Equal(160m, result.Cart.Total);
    }

    [Fact]
    public async Task Reading_active_cart_refreshes_quote_from_latest_catalog_facts()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.Sender.Send(new AddToCartCommand(
            SessionId: "read-refresh-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var firstRead = await app.Sender.Send(new GetCartQuery(
            SessionId: "read-refresh-session",
            CustomerId: null));
        Assert.NotNull(firstRead.Cart);
        Assert.Equal(100m, Assert.Single(firstRead.Cart!.Items).UnitPrice);

        await app.UpdateCatalogVariantAsync(item.VariantId, price: 125m, stockQuantity: 1);

        var refreshedRead = await app.Sender.Send(new GetCartQuery(
            SessionId: "read-refresh-session",
            CustomerId: null));

        Assert.NotNull(refreshedRead.Cart);
        var refreshedItem = Assert.Single(refreshedRead.Cart!.Items);
        Assert.Equal(125m, refreshedItem.UnitPrice);
        Assert.Equal(250m, refreshedItem.TotalPrice);
        Assert.Equal(1, refreshedItem.AvailableStock);
        Assert.False(refreshedItem.IsInStock);
        Assert.Equal(250m, refreshedRead.Cart.Subtotal);
        Assert.Equal(250m, refreshedRead.Cart.Total);
    }

    [Fact]
    public async Task Syncing_active_cart_refreshes_quote_from_latest_catalog_facts()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "sync-refresh-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 3));

        await app.UpdateCatalogVariantAsync(item.VariantId, price: 90m, stockQuantity: 2);

        var synced = await app.Sender.Send(new SyncCartPricesCommand(addResult.CartId));

        Assert.True(synced.Success);
        Assert.NotNull(synced.Cart);
        var syncedItem = Assert.Single(synced.Cart!.Items);
        Assert.Equal(90m, syncedItem.UnitPrice);
        Assert.Equal(270m, syncedItem.TotalPrice);
        Assert.Equal(2, syncedItem.AvailableStock);
        Assert.False(syncedItem.IsInStock);
        Assert.Equal(270m, synced.Cart.Subtotal);
        Assert.Equal(270m, synced.Cart.Total);
    }

    [Fact]
    public async Task Updating_cart_item_uses_latest_stock_and_quote_facts()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "update-refresh-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        await app.UpdateCatalogVariantAsync(item.VariantId, price: 120m, stockQuantity: 2);

        var updated = await app.Sender.Send(new UpdateCartItemCommand(
            addResult.CartId,
            item.VariantId,
            Quantity: 2));

        Assert.True(updated.Success, updated.ErrorMessage);
        Assert.NotNull(updated.Cart);
        var updatedItem = Assert.Single(updated.Cart!.Items);
        Assert.Equal(120m, updatedItem.UnitPrice);
        Assert.Equal(240m, updatedItem.TotalPrice);
        Assert.Equal(2, updatedItem.AvailableStock);
        Assert.True(updatedItem.IsInStock);

        await app.UpdateCatalogVariantAsync(item.VariantId, stockQuantity: 1);

        var rejected = await app.Sender.Send(new UpdateCartItemCommand(
            addResult.CartId,
            item.VariantId,
            Quantity: 2));

        Assert.False(rejected.Success);
        Assert.Contains("Chỉ còn 1", rejected.ErrorMessage);
    }

    [Fact]
    public async Task Cart_voucher_discounts_only_the_eligible_discount_base()
    {
        await using var app = new CommerceTestApp();
        var eligibleItem = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        var otherItem = await app.SeedCatalogItemAsync(price: 200m, stockQuantity: 5);
        await app.SeedVoucherAsync(
            code: "HALF-ELIGIBLE",
            type: VoucherType.Percentage,
            discountValue: 50m,
            applicableProductIds: [eligibleItem.ProductId]);

        var firstAdd = await app.Sender.Send(new AddToCartCommand(
            SessionId: "discount-base-session",
            CustomerId: null,
            ProductId: eligibleItem.ProductId,
            VariantId: eligibleItem.VariantId,
            Quantity: 1));

        await app.Sender.Send(new AddToCartCommand(
            SessionId: "discount-base-session",
            CustomerId: null,
            ProductId: otherItem.ProductId,
            VariantId: otherItem.VariantId,
            Quantity: 1));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(firstAdd.CartId, "half-eligible"));

        Assert.True(applied.Success, applied.ErrorMessage);
        Assert.NotNull(applied.Cart);
        Assert.Equal(300m, applied.Cart.Subtotal);
        Assert.Equal(50m, applied.DiscountAmount);
        Assert.Equal(50m, applied.Cart.VoucherDiscount);
        Assert.Equal(250m, applied.Cart.Total);
        Assert.Equal("HALF-ELIGIBLE", applied.Cart.AppliedVoucherCode);
    }

    [Fact]
    public async Task Applying_and_removing_cart_voucher_returns_refreshed_cart_quote()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        await app.SeedVoucherAsync(
            code: "QUOTE30",
            type: VoucherType.FixedAmount,
            discountValue: 30m);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "apply-remove-voucher-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, " quote30 "));

        Assert.True(applied.Success, applied.ErrorMessage);
        Assert.NotNull(applied.Cart);
        Assert.Equal(30m, applied.DiscountAmount);
        Assert.Equal("QUOTE30", applied.Cart!.AppliedVoucherCode);
        Assert.Equal(200m, applied.Cart.Subtotal);
        Assert.Equal(30m, applied.Cart.VoucherDiscount);
        Assert.Equal(170m, applied.Cart.Total);

        var checkoutWithVoucher = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
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

        Assert.Equal(170m, checkoutWithVoucher.Total);
        var voucherOrder = await app.Sender.Send(new GetOrderQuery(Id: checkoutWithVoucher.OrderId));
        Assert.Equal("QUOTE30", voucherOrder.VoucherCode);
        Assert.Equal(30m, voucherOrder.DiscountAmount);

        var secondAdd = await app.Sender.Send(new AddToCartCommand(
            SessionId: "remove-voucher-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));
        var secondApplied = await app.Sender.Send(new ApplyVoucherCommand(secondAdd.CartId, "quote30"));
        Assert.True(secondApplied.Success, secondApplied.ErrorMessage);

        var removed = await app.Sender.Send(new RemoveVoucherCommand(secondAdd.CartId));

        Assert.True(removed.Success);
        Assert.NotNull(removed.Cart);
        Assert.Null(removed.Cart!.AppliedVoucherCode);
        Assert.Equal(0m, removed.Cart.VoucherDiscount);
        Assert.Equal(200m, removed.Cart.Subtotal);
        Assert.Equal(200m, removed.Cart.Total);

        var checkoutWithoutVoucher = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: secondAdd.CartId,
            CustomerId: null,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.BankTransfer)));

        Assert.Equal(200m, checkoutWithoutVoucher.Total);
        var noVoucherOrder = await app.Sender.Send(new GetOrderQuery(Id: checkoutWithoutVoucher.OrderId));
        Assert.Null(noVoucherOrder.VoucherCode);
        Assert.Equal(0m, noVoucherOrder.DiscountAmount);
    }

    [Fact]
    public async Task Invalid_empty_and_missing_cart_voucher_application_fails_without_stale_state()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        await app.SeedVoucherAsync(
            code: "KEEP10",
            type: VoucherType.FixedAmount,
            discountValue: 10m);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "invalid-voucher-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var freshRejected = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "missing-code"));

        Assert.False(freshRejected.Success);
        Assert.Contains("không tồn tại", freshRejected.ErrorMessage);
        Assert.NotNull(freshRejected.Cart);
        Assert.Null(freshRejected.Cart!.AppliedVoucherCode);
        Assert.Equal(0m, freshRejected.Cart.VoucherDiscount);
        Assert.Equal(100m, freshRejected.Cart.Total);

        var valid = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "keep10"));
        Assert.True(valid.Success, valid.ErrorMessage);

        var rejected = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "missing-code"));

        Assert.False(rejected.Success);
        Assert.Contains("không tồn tại", rejected.ErrorMessage);
        Assert.Equal(0m, rejected.DiscountAmount);
        Assert.NotNull(rejected.Cart);
        Assert.Equal("KEEP10", rejected.Cart!.AppliedVoucherCode);
        Assert.Equal(10m, rejected.Cart.VoucherDiscount);
        Assert.Equal(90m, rejected.Cart.Total);

        var stillQuoted = await app.Sender.Send(new GetCartQuery(
            SessionId: "invalid-voucher-session",
            CustomerId: null,
            CartId: addResult.CartId));
        Assert.NotNull(stillQuoted.Cart);
        Assert.Equal("KEEP10", stillQuoted.Cart!.AppliedVoucherCode);
        Assert.Equal(10m, stillQuoted.Cart.VoucherDiscount);

        var emptyCart = await app.CreateEmptyCartAsync("empty-voucher-session");
        var emptyRejected = await app.Sender.Send(new ApplyVoucherCommand(emptyCart.Id, "keep10"));

        Assert.False(emptyRejected.Success);
        Assert.Equal("Cart is empty", emptyRejected.ErrorMessage);
        Assert.Null(emptyRejected.Cart);

        var missingRejected = await app.Sender.Send(new ApplyVoucherCommand(Guid.NewGuid(), "keep10"));

        Assert.False(missingRejected.Success);
        Assert.Equal("Cart not found", missingRejected.ErrorMessage);
        Assert.Null(missingRejected.Cart);
    }

    [Fact]
    public async Task Checkout_by_cart_id_consumes_cart_creates_order_attempt_and_redeems_voucher()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        await app.SeedVoucherAsync(
            code: "TAKE25",
            type: VoucherType.FixedAmount,
            discountValue: 25m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "order-attempt-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "take25"));
        Assert.True(applied.Success, applied.ErrorMessage);

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

        Assert.Equal(75m, checkout.Total);
        Assert.Equal("Pending", checkout.Status);

        var consumedCart = await app.Sender.Send(new GetCartQuery(
            SessionId: null,
            CustomerId: null,
            CartId: addResult.CartId));
        Assert.Null(consumedCart.Cart);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        var orderItem = Assert.Single(order.Items);
        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(25m, order.DiscountAmount);
        Assert.Equal(0m, order.ShippingFee);
        Assert.Equal(75m, order.Total);
        Assert.Equal("TAKE25", order.VoucherCode);
        Assert.Equal(item.ProductId, orderItem.ProductId);
        Assert.Equal(item.VariantId, orderItem.VariantId);
        Assert.Equal(100m, orderItem.UnitPrice);

        var voucherAfterCheckout = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "TAKE25",
            CartQuoteAmount: 100m));
        Assert.False(voucherAfterCheckout.IsValid);
        Assert.Empty(app.PaymentLinkAttempts);
    }

    [Fact]
    public async Task Free_shipping_voucher_zeroes_checkout_shipping_without_item_discount()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        await app.SeedVoucherAsync(
            code: "SHIPFREE",
            type: VoucherType.FreeShipping,
            discountValue: 0m);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "free-shipping-checkout-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "shipfree"));
        Assert.True(applied.Success, applied.ErrorMessage);
        Assert.NotNull(applied.Cart);
        Assert.Equal(0m, applied.DiscountAmount);
        Assert.Equal(0m, applied.Cart!.VoucherDiscount);
        Assert.True(applied.Cart.WaivesShipping);
        Assert.Equal(100m, applied.Cart.Total);

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerId: null,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ho Chi Minh City",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.CashOnDelivery)));

        Assert.Equal(100m, checkout.Total);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(0m, order.ShippingFee);
        Assert.Equal(100m, order.Total);
        Assert.Equal("SHIPFREE", order.VoucherCode);
    }

    [Fact]
    public async Task PayOS_checkout_requires_redirect_urls_before_creating_payment_link()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-url-validation-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
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
                PaymentMethod: PaymentMethod.PayOS))));

        Assert.Contains("ReturnUrl", ex.Message);
        Assert.Contains("CancelUrl", ex.Message);
        Assert.Empty(app.PaymentLinkAttempts);

        var orders = await app.Sender.Send(new GetOrdersQuery(PageSize: 100));
        Assert.Empty(orders.Orders);

        var activeCart = await app.Sender.Send(new GetCartQuery(
            SessionId: "payos-url-validation-session",
            CustomerId: null,
            CartId: addResult.CartId));
        Assert.NotNull(activeCart.Cart);
        Assert.Equal(100m, activeCart.Cart!.Total);
    }

    [Fact]
    public async Task PayOS_checkout_creates_payment_link_from_fresh_cart_quote_order_attempt()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "PAYOS20",
            type: VoucherType.FixedAmount,
            discountValue: 20m);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "payos-quote-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 2));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "payos20"));
        Assert.True(applied.Success, applied.ErrorMessage);

        await app.UpdateCatalogVariantAsync(item.VariantId, price: 125m, stockQuantity: 5);

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
            ReturnUrl: "https://shop.example/checkout/return?source=payos",
            CancelUrl: "https://shop.example/checkout/cancel")));

        Assert.Equal(230m, checkout.Total);
        Assert.Equal("Pending", checkout.Status);
        Assert.Equal("https://pay.example/checkout", checkout.PaymentUrl);

        var paymentAttempt = Assert.Single(app.PaymentLinkAttempts);
        var expectedReturnUrl = $"https://shop.example/checkout/return?source=payos&orderId={checkout.OrderId}";
        var expectedCancelUrl = $"https://shop.example/checkout/cancel?orderId={checkout.OrderId}";

        Assert.Equal(checkout.OrderId, paymentAttempt.OrderId);
        Assert.Equal(checkout.OrderNumber, paymentAttempt.OrderNumber);
        Assert.Equal(PaymentMethod.PayOS, paymentAttempt.PaymentMethod);
        Assert.Equal(250m, paymentAttempt.Subtotal);
        Assert.Equal(20m, paymentAttempt.DiscountAmount);
        Assert.Equal(0m, paymentAttempt.ShippingFee);
        Assert.Equal(230m, paymentAttempt.Total);
        Assert.Equal(expectedReturnUrl, paymentAttempt.ReturnUrl);
        Assert.Equal(expectedCancelUrl, paymentAttempt.CancelUrl);

        var paymentItem = Assert.Single(paymentAttempt.Items);
        Assert.Equal(item.ProductId, paymentItem.ProductId);
        Assert.Equal(item.VariantId, paymentItem.VariantId);
        Assert.Equal(125m, paymentItem.UnitPrice);
        Assert.Equal(2, paymentItem.Quantity);
        Assert.Equal(250m, paymentItem.TotalPrice);

        var order = await app.Sender.Send(new GetOrderQuery(Id: checkout.OrderId));
        Assert.Equal("PayOS", order.PaymentMethod);
        Assert.Equal(250m, order.Subtotal);
        Assert.Equal(20m, order.DiscountAmount);
        Assert.Equal(230m, order.Total);
        Assert.Equal("PAYOS20", order.VoucherCode);

        var paymentState = await app.GetOrderPaymentStateAsync(checkout.OrderId);
        Assert.Equal(123456789, paymentState.PayOsOrderCode);

        var consumedCart = await app.Sender.Send(new GetCartQuery(
            SessionId: "payos-quote-session",
            CustomerId: null,
            CartId: addResult.CartId));
        Assert.Null(consumedCart.Cart);
    }

    [Fact]
    public async Task NonPayOS_checkout_does_not_require_payos_redirect_urls_or_create_payment_link()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "bank-transfer-no-payos-url-session",
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

        Assert.Equal(100m, checkout.Total);
        Assert.Null(checkout.PaymentUrl);
        Assert.Empty(app.PaymentLinkAttempts);
    }

    [Fact]
    public async Task Checkout_rejects_legacy_item_payload_without_cart_id()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        var legacyPayload = $$"""
            {
              "customerId": null,
              "customerName": "Legacy Shopper",
              "customerEmail": "legacy@example.com",
              "customerPhone": "0900000000",
              "shippingAddress": "1 Test Street",
              "shippingCity": "Ha Noi",
              "shippingWard": "Ward 1",
              "shippingNote": null,
              "paymentMethod": 2,
              "voucherCode": "CLIENT25",
              "items": [
                {
                  "productId": "{{item.ProductId}}",
                  "variantId": "{{item.VariantId}}",
                  "quantity": 1,
                  "unitPrice": 1,
                  "totalPrice": 1
                }
              ],
              "subtotal": 1,
              "discountAmount": 25,
              "total": -24
            }
            """;

        var request = JsonSerializer.Deserialize<CheckoutRequest>(
            legacyPayload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(request);
        Assert.Equal(Guid.Empty, request!.CartId);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            app.Sender.Send(new CheckoutCommand(request)));

        Assert.Contains("CartId", ex.Message);

        var orders = await app.Sender.Send(new GetOrdersQuery(PageSize: 100));
        Assert.Empty(orders.Orders);
    }

    [Fact]
    public async Task Consumed_cart_cannot_be_mutated_or_reused_as_active_cart()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        const string sessionId = "consumed-cart-session";

        await app.SeedVoucherAsync(
            code: "CONSUME10",
            type: VoucherType.FixedAmount,
            discountValue: 10m);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: sessionId,
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "consume10"));
        Assert.True(applied.Success, applied.ErrorMessage);

        await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
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

        var persisted = await app.GetCartStateAsync(addResult.CartId);
        Assert.Equal(CartStatus.Consumed, persisted.Status);
        Assert.NotNull(persisted.ConsumedAt);
        Assert.Equal(0, persisted.ItemCount);
        Assert.Null(persisted.AppliedVoucherCode);
        Assert.Equal(0m, persisted.VoucherDiscount);

        var consumedRead = await app.Sender.Send(new GetCartQuery(
            SessionId: sessionId,
            CustomerId: null,
            CartId: addResult.CartId));
        Assert.Null(consumedRead.Cart);

        var update = await app.Sender.Send(new UpdateCartItemCommand(
            addResult.CartId,
            item.VariantId,
            Quantity: 2));
        Assert.False(update.Success);

        var remove = await app.Sender.Send(new RemoveFromCartCommand(addResult.CartId, item.VariantId));
        Assert.False(remove.Success);
        Assert.Null(remove.Cart);

        var clear = await app.Sender.Send(new ClearCartCommand(addResult.CartId));
        Assert.False(clear.Success);

        var removeVoucher = await app.Sender.Send(new RemoveVoucherCommand(addResult.CartId));
        Assert.False(removeVoucher.Success);
        Assert.Null(removeVoucher.Cart);

        var applyVoucher = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "consume10"));
        Assert.False(applyVoucher.Success);
        Assert.Null(applyVoucher.Cart);

        var sync = await app.Sender.Send(new SyncCartPricesCommand(addResult.CartId));
        Assert.False(sync.Success);
        Assert.Null(sync.Cart);

        var nextCart = await app.Sender.Send(new AddToCartCommand(
            SessionId: sessionId,
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        Assert.NotEqual(addResult.CartId, nextCart.CartId);
        Assert.Single(nextCart.Cart.Items);
    }

    [Fact]
    public async Task Merged_session_cart_can_checkout_with_the_customer_identity()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        const string sessionId = "merge-before-checkout-session";
        var customerId = Guid.NewGuid();

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: sessionId,
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
                CartId: addResult.CartId,
                CustomerId: customerId,
                CustomerName: "Merge Customer",
                CustomerEmail: "merge@example.test",
                CustomerPhone: "0900000000",
                ShippingAddress: "123 Test Street",
                ShippingCity: "Ho Chi Minh City",
                ShippingWard: "Ward 1",
                ShippingNote: null,
                PaymentMethod: PaymentMethod.BankTransfer))));
        Assert.Contains("CustomerId", mismatch.Message);

        var merged = await app.Sender.Send(new MergeCartsCommand(sessionId, customerId));

        Assert.True(merged.Success);
        Assert.NotNull(merged.Cart);
        Assert.Equal(addResult.CartId, merged.Cart!.Id);
        Assert.Equal(customerId, merged.Cart.CustomerId);
        Assert.Null(merged.Cart.SessionId);

        var checkout = await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: merged.Cart.Id,
            CustomerId: customerId,
            CustomerName: "Merge Customer",
            CustomerEmail: "merge@example.test",
            CustomerPhone: "0900000000",
            ShippingAddress: "123 Test Street",
            ShippingCity: "Ho Chi Minh City",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.BankTransfer)));

        Assert.Equal(100m, checkout.Total);
        var consumed = await app.GetCartStateAsync(merged.Cart.Id);
        Assert.Equal(CartStatus.Consumed, consumed.Status);
    }

    [Fact]
    public async Task Checkout_rejects_customer_mismatch_between_request_and_cart_quote()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        var cartCustomerId = Guid.NewGuid();
        var requestCustomerId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "ONE-EACH",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            configure: voucher => voucher.MaxUsagePerCustomer = 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: null,
            CustomerId: cartCustomerId,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "one-each"));
        Assert.True(applied.Success, applied.ErrorMessage);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
                CartId: addResult.CartId,
                CustomerId: requestCustomerId,
                CustomerName: "Jane Shopper",
                CustomerEmail: "jane@example.com",
                CustomerPhone: "0900000000",
                ShippingAddress: "1 Test Street",
                ShippingCity: "Ha Noi",
                ShippingWard: "Ward 1",
                ShippingNote: null,
                PaymentMethod: PaymentMethod.BankTransfer))));

        Assert.Contains("CustomerId", ex.Message);

        var orders = await app.Sender.Send(new GetOrdersQuery(PageSize: 100));
        Assert.Empty(orders.Orders);
    }

    [Fact]
    public async Task Checkout_does_not_keep_order_when_voucher_becomes_exhausted_after_cart_quote()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);

        await app.SeedVoucherAsync(
            code: "RACE25",
            type: VoucherType.FixedAmount,
            discountValue: 25m,
            maxUsageCount: 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: "stale-voucher-session",
            CustomerId: null,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "race25"));
        Assert.True(applied.Success, applied.ErrorMessage);

        app.OnReserveStock(async ct => await app.ExhaustVoucherAsync("RACE25", ct));

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

        Assert.Contains("hết lượt", ex.Message);

        var orders = await app.Sender.Send(new GetOrdersQuery(PageSize: 100));
        Assert.Empty(orders.Orders);

        var activeCart = await app.Sender.Send(new GetCartQuery(
            SessionId: "stale-voucher-session",
            CustomerId: null,
            CartId: addResult.CartId));

        Assert.NotNull(activeCart.Cart);
        Assert.Equal(100m, activeCart.Cart!.Subtotal);
        Assert.Null(activeCart.Cart.AppliedVoucherCode);
        Assert.Equal(100m, activeCart.Cart.Total);
    }

    [Fact]
    public async Task Checkout_records_voucher_usage_for_the_cart_quote_customer()
    {
        await using var app = new CommerceTestApp();
        var item = await app.SeedCatalogItemAsync(price: 100m, stockQuantity: 5);
        var cartCustomerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "ONE-CUSTOMER",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            configure: voucher => voucher.MaxUsagePerCustomer = 1);

        var addResult = await app.Sender.Send(new AddToCartCommand(
            SessionId: null,
            CustomerId: cartCustomerId,
            ProductId: item.ProductId,
            VariantId: item.VariantId,
            Quantity: 1));

        var applied = await app.Sender.Send(new ApplyVoucherCommand(addResult.CartId, "one-customer"));
        Assert.True(applied.Success, applied.ErrorMessage);

        await app.Sender.Send(new CheckoutCommand(new CheckoutRequest(
            CartId: addResult.CartId,
            CustomerId: cartCustomerId,
            CustomerName: "Jane Shopper",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "0900000000",
            ShippingAddress: "1 Test Street",
            ShippingCity: "Ha Noi",
            ShippingWard: "Ward 1",
            ShippingNote: null,
            PaymentMethod: PaymentMethod.BankTransfer)));

        var sameCustomer = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "ONE-CUSTOMER",
            CartQuoteAmount: 100m,
            CustomerId: cartCustomerId));
        var otherCustomer = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "ONE-CUSTOMER",
            CartQuoteAmount: 100m,
            CustomerId: otherCustomerId));

        Assert.False(sameCustomer.IsValid);
        Assert.Contains("hết lượt", sameCustomer.ErrorMessage);
        Assert.True(otherCustomer.IsValid, otherCustomer.ErrorMessage);
    }

    [Fact]
    public async Task Redeeming_same_order_attempt_and_voucher_is_idempotent()
    {
        await using var app = new CommerceTestApp();
        var firstCustomerId = Guid.NewGuid();
        var secondCustomerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "RETRY10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 2);

        var voucherId = await app.GetVoucherIdAsync("RETRY10");

        await app.Sender.Send(new RedeemVoucherCommand(voucherId, firstCustomerId, orderId, 10m));
        await app.Sender.Send(new RedeemVoucherCommand(voucherId, firstCustomerId, orderId, 10m));

        var stillAvailable = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "RETRY10",
            CartQuoteAmount: 100m,
            CustomerId: secondCustomerId));

        Assert.True(stillAvailable.IsValid, stillAvailable.ErrorMessage);

        await app.Sender.Send(new RedeemVoucherCommand(voucherId, secondCustomerId, Guid.NewGuid(), 10m));

        var exhausted = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "RETRY10",
            CartQuoteAmount: 100m));

        Assert.False(exhausted.IsValid);
        Assert.Contains("hết lượt", exhausted.ErrorMessage);
    }
}

internal sealed class CommerceTestApp : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private int _productCounter;

    public CommerceTestApp()
    {
        var services = new ServiceCollection();
        var databaseName = $"commerce-tests-{Guid.NewGuid():N}";

        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<ICacheInvalidator, MemoryCacheInvalidator>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(new HttpClient(new NoNetworkHttpMessageHandler()));

        services.AddTestDbContext<CartDbContext>(databaseName);
        services.AddTestDbContext<CustomerDbContext>(databaseName);
        services.AddTestDbContext<OrderDbContext>(databaseName);
        services.AddTestDbContext<ProductCatalogDbContext>(databaseName);
        services.AddTestDbContext<PromotionDbContext>(databaseName);

        services.AddMediatRWithAssemblies(
            typeof(CartModuleMarker).Assembly,
            typeof(CustomerModuleMarker).Assembly,
            typeof(OrderModule).Assembly,
            typeof(ProductCatalogModule).Assembly,
            typeof(PromotionModuleMarker).Assembly);

        var cartQuoteServiceType = typeof(CartModuleMarker).Assembly
            .GetType("Cart.ShoppingCarts.Services.CartQuoteService")
            ?? throw new InvalidOperationException("CartQuoteService was not found.");
        services.AddScoped(cartQuoteServiceType);

        services.AddScoped<IStockReservationService, TestStockReservationService>();
        services.AddScoped<IPaymentService, TestPaymentService>();
        services.AddOrderIntake();

        _provider = services.BuildServiceProvider(validateScopes: true);
        _scope = _provider.CreateAsyncScope();
    }

    public ISender Sender => _scope.ServiceProvider.GetRequiredService<ISender>();

    public IReadOnlyList<PaymentLinkAttempt> PaymentLinkAttempts
        => ((TestPaymentService)_scope.ServiceProvider.GetRequiredService<IPaymentService>()).Attempts;

    public IReadOnlyList<StockConfirmation> StockConfirmations
        => ((TestStockReservationService)_scope.ServiceProvider.GetRequiredService<IStockReservationService>()).Confirmations;

    public async Task<CatalogItem> SeedCatalogItemAsync(decimal price, int stockQuantity, Guid? categoryId = null)
    {
        var catalog = _scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();
        var index = ++_productCounter;
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = $"Test brand {index}",
            UrlSlug = $"test-brand-{index}"
        };
        var category = new Category
        {
            Id = categoryId ?? Guid.NewGuid(),
            Name = $"Test category {index}",
            UrlSlug = $"test-category-{index}"
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test sneaker",
            UrlSlug = $"test-sneaker-{index}",
            Description = "A product seeded for Cart Quote tests",
            BrandId = brand.Id,
            CategoryId = category.Id,
            IsActive = true,
            IsDeleted = false
        };
        var variant = new Variant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Sku = $"SKU-{index}",
            Description = "Size 42",
            Price = price,
            StockQuantity = stockQuantity,
            IsActive = true,
            IsDeleted = false
        };

        catalog.Brands.Add(brand);
        catalog.Categories.Add(category);
        catalog.Products.Add(product);
        catalog.Variants.Add(variant);
        await catalog.SaveChangesAsync();

        return new CatalogItem(product.Id, variant.Id, category.Id, variant.Sku);
    }

    public async Task UpdateCatalogVariantAsync(
        Guid variantId,
        decimal? price = null,
        int? stockQuantity = null)
    {
        var catalog = _scope.ServiceProvider.GetRequiredService<ProductCatalogDbContext>();
        var variant = await catalog.Variants.SingleAsync(v => v.Id == variantId);

        if (price.HasValue)
            variant.Price = price.Value;

        if (stockQuantity.HasValue)
            variant.StockQuantity = stockQuantity.Value;

        await catalog.SaveChangesAsync();
    }

    public async Task SeedSaleAsync(CatalogItem item, decimal salePrice, decimal? originalPrice = null)
    {
        var promotion = _scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var campaign = new SaleCampaign
        {
            Id = Guid.NewGuid(),
            Name = "Active sale",
            Slug = $"active-sale-{Guid.NewGuid():N}",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true
        };
        var saleItem = new SaleCampaignItem
        {
            Id = Guid.NewGuid(),
            SaleCampaignId = campaign.Id,
            ProductId = item.ProductId,
            VariantId = item.VariantId,
            SalePrice = salePrice,
            OriginalPrice = originalPrice
        };

        promotion.SaleCampaigns.Add(campaign);
        promotion.SaleCampaignItems.Add(saleItem);
        await promotion.SaveChangesAsync();
    }

    public async Task SeedVoucherAsync(
        string code,
        VoucherType type,
        decimal discountValue,
        List<Guid>? applicableProductIds = null,
        Guid? applicableCategoryId = null,
        int? maxUsageCount = null,
        Action<Voucher>? configure = null)
    {
        var promotion = _scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            Code = code.ToUpperInvariant(),
            Type = type,
            DiscountValue = discountValue,
            ApplicableProductIds = applicableProductIds,
            ApplicableCategoryId = applicableCategoryId,
            MaxUsageCount = maxUsageCount,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            IsActive = true
        };
        configure?.Invoke(voucher);

        promotion.Vouchers.Add(voucher);
        await promotion.SaveChangesAsync();
    }

    public void OnReserveStock(Func<CancellationToken, Task> onReserve)
    {
        var stockReservation = (TestStockReservationService)_scope.ServiceProvider.GetRequiredService<IStockReservationService>();
        stockReservation.OnReserveAsync = onReserve;
    }

    public async Task ExhaustVoucherAsync(string code, CancellationToken ct = default)
    {
        var promotion = _scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var voucher = await promotion.Vouchers.SingleAsync(
            v => v.Code == code.ToUpperInvariant(),
            ct);

        voucher.CurrentUsageCount = voucher.MaxUsageCount ?? voucher.CurrentUsageCount + 1;
        voucher.UpdatedAt = DateTime.UtcNow;
        await promotion.SaveChangesAsync(ct);
    }

    public async Task<Guid> GetVoucherIdAsync(string code, CancellationToken ct = default)
    {
        var promotion = _scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        return await promotion.Vouchers
            .Where(v => v.Code == code.ToUpperInvariant())
            .Select(v => v.Id)
            .SingleAsync(ct);
    }

    public async Task<ShoppingCart> CreateEmptyCartAsync(string sessionId, CancellationToken ct = default)
    {
        var cartDb = _scope.ServiceProvider.GetRequiredService<CartDbContext>();
        var cart = new ShoppingCart
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Status = CartStatus.Active
        };

        cartDb.ShoppingCarts.Add(cart);
        await cartDb.SaveChangesAsync(ct);

        return cart;
    }

    public async Task<CartState> GetCartStateAsync(Guid cartId, CancellationToken ct = default)
    {
        var cartDb = _scope.ServiceProvider.GetRequiredService<CartDbContext>();
        var cart = await cartDb.ShoppingCarts
            .Include(c => c.Items)
            .AsNoTracking()
            .SingleAsync(c => c.Id == cartId, ct);

        return new CartState(
            cart.Status,
            cart.ConsumedAt,
            cart.Items.Count,
            cart.AppliedVoucherCode,
            cart.VoucherDiscount);
    }

    public async Task<OrderPaymentState> GetOrderPaymentStateAsync(Guid orderId, CancellationToken ct = default)
    {
        var orderDb = _scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await orderDb.Orders
            .AsNoTracking()
            .SingleAsync(o => o.Id == orderId, ct);

        return new OrderPaymentState(order.PayOsOrderCode);
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }
}

internal sealed record CatalogItem(Guid ProductId, Guid VariantId, Guid CategoryId, string Sku);

internal sealed record CartState(
    CartStatus Status,
    DateTime? ConsumedAt,
    int ItemCount,
    string? AppliedVoucherCode,
    decimal VoucherDiscount);

internal sealed record OrderPaymentState(long? PayOsOrderCode);

internal sealed record StockConfirmation(string SessionKey, Guid OrderId);

internal sealed record PaymentLinkAttempt(
    Guid OrderId,
    string OrderNumber,
    PaymentMethod PaymentMethod,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal Total,
    string ReturnUrl,
    string CancelUrl,
    IReadOnlyList<PaymentLinkAttemptItem> Items);

internal sealed record PaymentLinkAttemptItem(
    Guid ProductId,
    Guid VariantId,
    decimal UnitPrice,
    int Quantity,
    decimal TotalPrice);

internal static class TestServiceCollectionExtensions
{
    public static IServiceCollection AddTestDbContext<TContext>(
        this IServiceCollection services,
        string databaseName)
        where TContext : DbContext
    {
        return services.AddDbContext<TContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        });
    }
}

internal sealed class TestStockReservationService : IStockReservationService
{
    private readonly List<StockConfirmation> _confirmations = [];

    public Func<CancellationToken, Task>? OnReserveAsync { get; set; }
    public IReadOnlyList<StockConfirmation> Confirmations => _confirmations;

    public async Task<List<Guid>> ReserveStockAsync(
        string sessionKey,
        List<(Guid VariantId, int Quantity)> items,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        if (OnReserveAsync is not null)
            await OnReserveAsync(ct);

        return items.Select(_ => Guid.NewGuid()).ToList();
    }

    public Task ConfirmReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default)
    {
        _confirmations.Add(new StockConfirmation(sessionKey, orderId));
        return Task.CompletedTask;
    }

    public Task ReleaseReservationsAsync(string sessionKey, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task RestoreConfirmedReservationsAsync(string sessionKey, Guid orderId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<int> CleanupExpiredReservationsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(0);
    }
}

internal sealed class TestPaymentService : IPaymentService
{
    private readonly List<PaymentLinkAttempt> _attempts = [];

    public IReadOnlyList<PaymentLinkAttempt> Attempts => _attempts;

    public Task<PaymentLinkResult> CreatePaymentLinkAsync(
        Order.Orders.Models.CustomerOrder order,
        string returnUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        _attempts.Add(new PaymentLinkAttempt(
            order.Id,
            order.OrderNumber,
            order.PaymentMethod,
            order.Subtotal,
            order.DiscountAmount,
            order.ShippingFee,
            order.Total,
            returnUrl,
            cancelUrl,
            order.Items.Select(item => new PaymentLinkAttemptItem(
                item.ProductId,
                item.VariantId,
                item.UnitPrice,
                item.Quantity,
                item.TotalPrice)).ToList()));

        return Task.FromResult(new PaymentLinkResult("https://pay.example/checkout", 123456789));
    }

    public Task<WebhookResult> HandleWebhookAsync(Webhook webhookPayload, CancellationToken ct = default)
    {
        return Task.FromResult(new WebhookResult(123456789, true, "PAID"));
    }
}

internal sealed class NoNetworkHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Tests should not call external HTTP services.");
    }
}
