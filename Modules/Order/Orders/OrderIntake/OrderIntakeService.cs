using Cart.ShoppingCarts.Features.QuoteAndConsumeCart;
using Cart.ShoppingCarts.Services;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Order.Orders.Events;
using Order.Orders.Features.CalculateShipping;
using Order.Orders.Services;
using ProductCatalog.Products.Services;
using Promotion.Vouchers.Features.ManageVoucherUsage;

namespace Order.Orders.OrderIntake;

public interface IOrderIntake
{
    Task<StartOrderAttemptResult> StartOrderAttemptAsync(
        StartOrderAttempt request,
        CancellationToken cancellationToken = default);

    Task<ResolvePayOsPaymentResultResult> ResolvePayOsPaymentResultAsync(
        PayOsPaymentResult result,
        CancellationToken cancellationToken = default);

    Task<bool> ExpirePayOsOrderAttemptIfSettlementGraceEndedAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<CancelOrderAttemptResult> CancelOrderAttemptAsync(
        CancelOrderAttempt request,
        CancellationToken cancellationToken = default);
}

public record StartOrderAttempt(
    Guid CartId,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ShippingAddress,
    string ShippingCity,
    string ShippingWard,
    string? ShippingNote,
    PaymentMethod PaymentMethod,
    string? ReturnUrl = null,
    string? CancelUrl = null,
    string? ShippingDistrict = null);

public record StartOrderAttemptResult(
    Guid OrderId,
    string OrderNumber,
    decimal Total,
    string Status,
    string? PaymentUrl = null);

public record CancelOrderAttempt(
    Guid OrderId,
    Guid? RequestUserId,
    string? RequestEmail,
    string? Reason = null);

public record CancelOrderAttemptResult(
    Guid OrderId,
    string OrderNumber,
    string Status,
    string PaymentStatus,
    DateTime CancelledAt,
    string? CancellationReason);

public record PayOsPaymentResult(
    long OrderCode,
    PayOsPaymentResultStatus Status,
    string? ProviderStatus = null);

public enum PayOsPaymentResultStatus
{
    Paid,
    Failed,
    Cancelled,
    Pending,
    Processing
}

public record ResolvePayOsPaymentResultResult(
    Guid? OrderId,
    PayOsPaymentResolution Resolution);

public enum PayOsPaymentResolution
{
    UnknownOrder,
    Waiting,
    Confirmed,
    Failed,
    Cancelled,
    Expired,
    AlreadyResolved,
    LatePaidAfterGrace
}

public static class OrderIntakeServiceCollectionExtensions
{
    public static IServiceCollection AddOrderIntake(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(OrderIntakePaymentWindowPolicy.Default);

        return services.AddScoped<IOrderIntake, OrderIntake>();
    }
}

internal class OrderIntake(
    OrderDbContext orderDb,
    IStockReservationService stockReservation,
    IPaymentService paymentService,
    ISender sender,
    TimeProvider timeProvider,
    OrderIntakePaymentWindowPolicy paymentWindowPolicy)
    : IOrderIntake
{
    public async Task<StartOrderAttemptResult> StartOrderAttemptAsync(
        StartOrderAttempt request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var existingAttempt = await FindLatestAttemptForCartAsync(
            request.CartId,
            cancellationToken);

        if (existingAttempt is not null)
        {
            if (existingAttempt.CreatedOrderAt is not null)
                return ToStartResult(existingAttempt);

            if (existingAttempt.PaymentMethod == PaymentMethod.PayOS)
            {
                if (CanReusePayOsAttempt(existingAttempt, now.UtcDateTime))
                {
                    return ToStartResult(existingAttempt);
                }

                await ExpirePayOsOrderAttemptIfSettlementGraceEndedAsync(
                    existingAttempt,
                    now.UtcDateTime,
                    cancellationToken);

                throw new InvalidOperationException(
                    "Payment Window is no longer live for this Order Attempt. Return to the cart for a fresh Cart Quote.");
            }

            throw new InvalidOperationException(
                "An uncreated Order Attempt already exists for this cart.");
        }

        var quote = (await sender.Send(
            new QuoteCartForCheckoutCommand(request.CartId, request.CustomerId),
            cancellationToken)).Quote;
        var cartSnapshot = ToRestoreCartCommand(quote);

        var orderItems = quote.Items.Select(item => new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = item.ProductId,
            VariantId = item.VariantId,
            ProductName = item.ProductName,
            VariantSku = item.VariantSku,
            VariantDescription = item.VariantDescription,
            ImageUrl = item.ImageUrl,
            UnitPrice = item.UnitPrice,
            CompareAtPrice = item.CompareAtPrice,
            Quantity = item.Quantity,
            TotalPrice = item.TotalPrice
        }).ToList();

        var shippingResult = await sender.Send(new CalculateShippingQuery(
            request.PaymentMethod,
            request.ShippingCity,
            request.ShippingWard,
            quote.Items.Select(i => new CalculateShippingItem(i.ProductId, i.VariantId, i.Quantity)).ToList(),
            request.ShippingDistrict
        ), cancellationToken);

        var shippingFee = quote.WaivesShipping ? 0 : shippingResult.ShippingFee;
        var subtotal = quote.Subtotal;
        var discountAmount = quote.VoucherDiscount;
        var total = subtotal - discountAmount + shippingFee;

        var paymentWindowExpiresAt = request.PaymentMethod == PaymentMethod.PayOS
            ? paymentWindowPolicy.GetPaymentWindowEnd(now)
            : (DateTimeOffset?)null;
        var paymentSettlementGraceEndsAt = paymentWindowExpiresAt.HasValue
            ? paymentWindowPolicy.GetSettlementGraceEnd(paymentWindowExpiresAt.Value)
            : (DateTimeOffset?)null;

        var order = new CustomerOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(),
            SourceCartId = request.CartId,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            ShippingAddress = request.ShippingAddress,
            ShippingCity = request.ShippingCity,
            ShippingWard = !string.IsNullOrWhiteSpace(request.ShippingDistrict)
                ? $"{request.ShippingDistrict}, {request.ShippingWard}"
                : request.ShippingWard,
            ShippingNote = request.ShippingNote,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            ShippingFee = shippingFee,
            Total = total,
            VoucherId = quote.AppliedVoucherId,
            VoucherCode = quote.AppliedVoucherCode,
            PaymentMethod = request.PaymentMethod,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            Items = orderItems
        };

        if (request.PaymentMethod == PaymentMethod.PayOS)
        {
            order.PaymentWindowExpiresAt = paymentWindowExpiresAt!.Value.UtcDateTime;
            order.PaymentSettlementGraceEndsAt = paymentSettlementGraceEndsAt!.Value.UtcDateTime;
        }

        var sessionKey = $"order:{order.Id}";
        order.ReservationSessionKey = sessionKey;

        var stockHoldTtl = request.PaymentMethod == PaymentMethod.PayOS
            ? paymentSettlementGraceEndsAt!.Value - now
            : (TimeSpan?)null;
        var stockLines = quote.Items
            .Select(i => new StockLifecycleLine(i.VariantId, i.Quantity))
            .ToList();

        var stockReserved = false;

        try
        {
            if (request.PaymentMethod == PaymentMethod.PayOS)
            {
                var holdResult = await sender.Send(new HoldOrderAttemptStockCommand(
                    order.Id,
                    stockLines,
                    paymentSettlementGraceEndsAt!.Value.UtcDateTime), cancellationToken);
                EnsureStockLifecycleSuccess(holdResult);
            }
            else
            {
                await stockReservation.ReserveStockAsync(
                    sessionKey,
                    quote.Items.Select(i => (i.VariantId, i.Quantity)).ToList(),
                    stockHoldTtl,
                    cancellationToken);
            }

            stockReserved = true;

            orderDb.Orders.Add(order);
            await orderDb.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (stockReserved)
                await ReleaseOrderAttemptStockAsync(order, cancellationToken);

            DetachOrderAttempt(order);
            throw;
        }

        try
        {
            if (order.VoucherId.HasValue)
            {
                await sender.Send(new HoldVoucherUsageCommand(
                    order.VoucherId.Value,
                    request.CustomerId,
                    order.Id,
                    discountAmount), cancellationToken);
            }
        }
        catch
        {
            await ReleaseOrderAttemptStockAsync(order, cancellationToken);
            await RemoveOrderAttemptRecordAsync(order, cancellationToken);
            throw;
        }

        string? paymentUrl = null;

        if (request.PaymentMethod is PaymentMethod.CashOnDelivery or PaymentMethod.BankTransfer)
        {
            var cartConsumed = false;
            var stockConfirmed = false;

            try
            {
                await sender.Send(new ConsumeQuotedCartCommand(request.CartId, request.CustomerId), cancellationToken);
                cartConsumed = true;

                await stockReservation.ConfirmReservationsAsync(sessionKey, order.Id, cancellationToken);
                stockConfirmed = true;

                if (order.VoucherId.HasValue)
                {
                    await sender.Send(new FinalizeVoucherUsageCommand(
                        order.VoucherId.Value,
                        order.Id), cancellationToken);
                }

                order.CreatedOrderAt ??= timeProvider.GetUtcNow().UtcDateTime;
                order.AddDomainEvent(OrderCreatedEventFactory.FromOrder(order));
                await orderDb.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                order.ClearDomainEvents();

                if (order.VoucherId.HasValue)
                    await ReleaseVoucherHoldAsync(order, cancellationToken);

                if (stockConfirmed)
                {
                    await stockReservation.RestoreConfirmedReservationsAsync(
                        sessionKey,
                        order.Id,
                        cancellationToken);
                }
                else
                {
                    await ReleaseOrderAttemptStockAsync(order, cancellationToken);
                }

                if (cartConsumed)
                    await sender.Send(cartSnapshot, cancellationToken);

                await RemoveOrderAttemptRecordAsync(order, cancellationToken);
                throw;
            }
        }

        if (request.PaymentMethod == PaymentMethod.PayOS)
        {
            var cartConsumed = false;

            try
            {
                await sender.Send(new ConsumeQuotedCartCommand(request.CartId, request.CustomerId), cancellationToken);
                cartConsumed = true;

                var returnUrl = AppendOrderIdToUrl(
                    request.ReturnUrl ?? throw new InvalidOperationException("ReturnUrl is required for PayOS"),
                    order.Id);
                var cancelUrl = AppendOrderIdToUrl(
                    request.CancelUrl ?? throw new InvalidOperationException("CancelUrl is required for PayOS"),
                    order.Id);

                var payResult = await paymentService.CreatePaymentLinkAsync(
                    order, returnUrl, cancelUrl, paymentWindowExpiresAt!.Value, cancellationToken);

                order.PayOsOrderCode = payResult.OrderCode;
                order.PaymentLinkId = payResult.PaymentLinkId;
                order.PaymentLinkUrl = payResult.CheckoutUrl;
                paymentUrl = payResult.CheckoutUrl;

                await orderDb.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                Exception? paymentLinkCancellationFailure = null;

                if (order.PayOsOrderCode.HasValue)
                {
                    try
                    {
                        await paymentService.CancelPaymentLinkAsync(
                            order.PayOsOrderCode.Value,
                            "Order Intake failed before the PayOS Order Attempt could be returned.",
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        paymentLinkCancellationFailure = ex;
                    }
                }

                if (cartConsumed)
                    await sender.Send(cartSnapshot, cancellationToken);

                await RemoveUncreatedOrderAttemptAsync(order, cancellationToken);

                if (paymentLinkCancellationFailure is not null)
                {
                    throw new InvalidOperationException(
                        "PayOS payment-link cancellation failed during Order Intake compensation.",
                        paymentLinkCancellationFailure);
                }

                throw;
            }
        }

        return new StartOrderAttemptResult(
            order.Id,
            order.OrderNumber,
            order.Total,
            order.Status.ToString(),
            paymentUrl);
    }

    public async Task<ResolvePayOsPaymentResultResult> ResolvePayOsPaymentResultAsync(
        PayOsPaymentResult result,
        CancellationToken cancellationToken = default)
    {
        var order = await orderDb.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.PayOsOrderCode == result.OrderCode, cancellationToken);

        if (order is null)
            return new ResolvePayOsPaymentResultResult(null, PayOsPaymentResolution.UnknownOrder);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (order.CreatedOrderAt is not null)
            return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.AlreadyResolved);

        if (result.Status == PayOsPaymentResultStatus.Paid && IsAfterSettlementGrace(order, now))
        {
            await FailPayOsOrderAttemptAsync(
                order,
                now,
                "Late PayOS paid result after settlement grace; manual review required",
                cancellationToken);

            return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.LatePaidAfterGrace);
        }

        if (IsTerminalUncreatedPayOsAttempt(order))
            return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.AlreadyResolved);

        if (result.Status == PayOsPaymentResultStatus.Failed)
        {
            await FailPayOsOrderAttemptAsync(
                order,
                now,
                "Thanh toán PayOS thất bại",
                cancellationToken);

            return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.Failed);
        }

        if (result.Status == PayOsPaymentResultStatus.Cancelled)
        {
            await FailPayOsOrderAttemptAsync(
                order,
                now,
                "Thanh toán PayOS bị hủy",
                cancellationToken);

            return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.Cancelled);
        }

        if (result.Status is PayOsPaymentResultStatus.Pending or PayOsPaymentResultStatus.Processing)
        {
            if (IsAfterSettlementGrace(order, now))
            {
                await FailPayOsOrderAttemptAsync(
                    order,
                    now,
                    "Quá thời gian thanh toán PayOS",
                    cancellationToken);

                return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.Expired);
            }

            return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.Waiting);
        }

        var shouldPublishCreatedOrder = order.CreatedOrderAt is null;

        var stockConfirmed = false;
        var voucherFinalized = false;

        try
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt ??= now;
            order.Status = OrderStatus.Confirmed;

            if (!string.IsNullOrEmpty(order.ReservationSessionKey))
            {
                await stockReservation.ConfirmReservationsAsync(
                    order.ReservationSessionKey,
                    order.Id,
                    cancellationToken);
                stockConfirmed = true;
            }

            if (order.VoucherId.HasValue)
            {
                await sender.Send(new FinalizeVoucherUsageCommand(
                    order.VoucherId.Value,
                    order.Id), cancellationToken);
                voucherFinalized = true;
            }

            order.CreatedOrderAt ??= now;
            if (shouldPublishCreatedOrder)
                order.AddDomainEvent(OrderCreatedEventFactory.FromOrder(order));

            await orderDb.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            order.ClearDomainEvents();

            if (voucherFinalized)
                await ReleaseVoucherHoldAsync(order, cancellationToken);

            if (stockConfirmed && !string.IsNullOrEmpty(order.ReservationSessionKey))
            {
                await stockReservation.RestoreConfirmedReservationsAsync(
                    order.ReservationSessionKey,
                    order.Id,
                    cancellationToken);
            }

            await ReloadOrderAttemptAsync(order, cancellationToken);
            throw;
        }

        return new ResolvePayOsPaymentResultResult(order.Id, PayOsPaymentResolution.Confirmed);
    }

    public async Task<bool> ExpirePayOsOrderAttemptIfSettlementGraceEndedAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await orderDb.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            throw new KeyNotFoundException("Order not found");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await ExpirePayOsOrderAttemptIfSettlementGraceEndedAsync(
            order,
            now,
            cancellationToken);
    }

    public async Task<CancelOrderAttemptResult> CancelOrderAttemptAsync(
        CancelOrderAttempt request,
        CancellationToken cancellationToken = default)
    {
        var order = await orderDb.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            throw new KeyNotFoundException("Order not found");

        if (!IsOrderOwner(order, request.RequestUserId, request.RequestEmail))
            throw new UnauthorizedAccessException("Ban khong co quyen huy don hang nay.");

        if (order.PaymentMethod != PaymentMethod.PayOS)
            throw new InvalidOperationException("Order Intake cancellation only applies to PayOS Order Attempts.");

        if (order.CreatedOrderAt is not null)
            throw new InvalidOperationException("Created Orders must be cancelled through the broader order workflow.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (await ExpirePayOsOrderAttemptIfSettlementGraceEndedAsync(order, now, cancellationToken))
        {
            return ToCancelResult(order);
        }

        if (IsInSettlementGrace(order, now))
            throw new InvalidOperationException(
                "PayOS Order Attempt is in settlement grace; wait for a verified provider result or grace end.");

        if (order.Status != OrderStatus.Pending || order.PaymentStatus != PaymentStatus.Pending)
            throw new InvalidOperationException("Don hang PayOS khong con o trang thai cho thanh toan de huy.");

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Khach huy don hang truoc khi xac nhan"
            : request.Reason;

        await FailPayOsOrderAttemptAsync(order, now, reason, cancellationToken);

        return ToCancelResult(order);
    }

    private static CancelOrderAttemptResult ToCancelResult(CustomerOrder order)
    {
        return new CancelOrderAttemptResult(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.PaymentStatus.ToString(),
            order.CancelledAt ?? throw new InvalidOperationException("CancelledAt is required for a cancelled Order Attempt."),
            order.CancellationReason);
    }

    private async Task<bool> ExpirePayOsOrderAttemptIfSettlementGraceEndedAsync(
        CustomerOrder order,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!IsPendingUncreatedPayOsAttempt(order) || !IsAfterSettlementGrace(order, now))
            return false;

        await FailPayOsOrderAttemptAsync(
            order,
            now,
            "Quá thời gian thanh toán PayOS",
            cancellationToken);

        return true;
    }

    private async Task FailPayOsOrderAttemptAsync(
        CustomerOrder order,
        DateTime now,
        string reason,
        CancellationToken cancellationToken)
    {
        if (order.Status == OrderStatus.Cancelled && order.PaymentStatus == PaymentStatus.Failed)
            return;

        order.PaymentStatus = PaymentStatus.Failed;
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt ??= now;
        order.CancellationReason ??= reason;

        await ReleaseOrderAttemptStockAsync(order, cancellationToken);

        if (order.VoucherId.HasValue && order.CreatedOrderAt is null)
        {
            await sender.Send(new ReleaseVoucherUsageCommand(
                order.VoucherId.Value,
                order.Id), cancellationToken);
        }

        await orderDb.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveUncreatedOrderAttemptAsync(
        CustomerOrder order,
        CancellationToken cancellationToken)
    {
        await ReleaseOrderAttemptStockAsync(order, cancellationToken);

        if (order.VoucherId.HasValue)
        {
            await ReleaseVoucherHoldAsync(order, cancellationToken);
        }

        await RemoveOrderAttemptRecordAsync(order, cancellationToken);
    }

    private async Task ReleaseVoucherHoldAsync(
        CustomerOrder order,
        CancellationToken cancellationToken)
    {
        if (!order.VoucherId.HasValue)
            return;

        await sender.Send(new ReleaseVoucherUsageCommand(
            order.VoucherId.Value,
            order.Id), cancellationToken);
    }

    private async Task ReleaseOrderAttemptStockAsync(
        CustomerOrder order,
        CancellationToken cancellationToken)
    {
        if (order.PaymentMethod == PaymentMethod.PayOS)
        {
            var result = await sender.Send(
                new ReleaseOrderAttemptStockCommand(order.Id),
                cancellationToken);
            EnsureStockLifecycleSuccess(result, "release this Order Attempt's Stock Hold");
            return;
        }

        if (!string.IsNullOrEmpty(order.ReservationSessionKey))
            await stockReservation.ReleaseReservationsAsync(
                order.ReservationSessionKey,
                cancellationToken);
    }

    private static void EnsureStockLifecycleSuccess(
        StockLifecycleResult result,
        string operationDescription = "hold this Order Attempt")
    {
        if (result.IsSuccess)
            return;

        var message = result.Status switch
        {
            StockLifecycleResultStatus.ValidationFailed => string.Join(
                " ",
                result.ValidationDetails.Select(detail => detail.Message)),
            StockLifecycleResultStatus.InsufficientStock => string.Join(
                " ",
                result.InsufficientStockDetails.Select(detail =>
                    $"Insufficient stock for Product Variant {detail.ProductVariantId}. Requested {detail.RequestedQuantity}, available {detail.AvailableQuantity}.")),
            StockLifecycleResultStatus.Conflict => result.Conflict?.Reason
                ?? "Stock lifecycle conflict for this Order Attempt.",
            _ => $"Stock lifecycle could not {operationDescription}."
        };

        throw new InvalidOperationException(message);
    }

    private async Task RemoveOrderAttemptRecordAsync(
        CustomerOrder order,
        CancellationToken cancellationToken)
    {
        order.ClearDomainEvents();
        orderDb.Orders.Remove(order);
        await orderDb.SaveChangesAsync(cancellationToken);
    }

    private async Task ReloadOrderAttemptAsync(
        CustomerOrder order,
        CancellationToken cancellationToken)
    {
        try
        {
            await orderDb.Entry(order).ReloadAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            orderDb.Entry(order).State = EntityState.Detached;
        }
    }

    private void DetachOrderAttempt(CustomerOrder order)
    {
        var entry = orderDb.Entry(order);
        if (entry.State != EntityState.Detached)
            entry.State = EntityState.Detached;
    }

    private static RestoreConsumedCartCommand ToRestoreCartCommand(CartQuote quote)
    {
        return new RestoreConsumedCartCommand(
            quote.CartId,
            quote.CustomerId,
            quote.Items.Select(item => new RestoreConsumedCartItem(
                item.ProductId,
                item.VariantId,
                item.ProductName,
                item.VariantSku,
                item.VariantDescription,
                item.ImageUrl,
                item.UnitPrice,
                item.CompareAtPrice,
                item.Quantity,
                item.AvailableStock,
                item.IsInStock)).ToList(),
            quote.AppliedVoucherId,
            quote.AppliedVoucherCode,
            quote.VoucherDiscount);
    }

    private static bool IsOrderOwner(CustomerOrder order, Guid? requestUserId, string? requestEmail)
    {
        if (requestUserId.HasValue && order.CustomerId == requestUserId.Value)
            return true;

        if (!string.IsNullOrWhiteSpace(requestEmail)
            && string.Equals(order.CustomerEmail, requestEmail, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsAfterSettlementGrace(CustomerOrder order, DateTime now)
    {
        return order.PaymentSettlementGraceEndsAt.HasValue
            && now > order.PaymentSettlementGraceEndsAt.Value;
    }

    private static bool IsInSettlementGrace(CustomerOrder order, DateTime now)
    {
        return order.PaymentWindowExpiresAt.HasValue
            && order.PaymentSettlementGraceEndsAt.HasValue
            && now > order.PaymentWindowExpiresAt.Value
            && now <= order.PaymentSettlementGraceEndsAt.Value;
    }

    private static bool IsPendingUncreatedPayOsAttempt(CustomerOrder order)
    {
        return order.PaymentMethod == PaymentMethod.PayOS
            && order.CreatedOrderAt is null
            && order.Status == OrderStatus.Pending
            && order.PaymentStatus == PaymentStatus.Pending;
    }

    private static bool IsTerminalUncreatedPayOsAttempt(CustomerOrder order)
    {
        return order.PaymentMethod == PaymentMethod.PayOS
            && order.CreatedOrderAt is null
            && (order.Status == OrderStatus.Cancelled || order.PaymentStatus == PaymentStatus.Failed);
    }

    private async Task<CustomerOrder?> FindLatestAttemptForCartAsync(
        Guid cartId,
        CancellationToken cancellationToken)
    {
        return await orderDb.Orders
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(o =>
                o.SourceCartId == cartId,
                cancellationToken);
    }

    private static bool CanReusePayOsAttempt(CustomerOrder order, DateTime now)
    {
        return order.Status == OrderStatus.Pending
            && order.PaymentStatus == PaymentStatus.Pending
            && order.PaymentWindowExpiresAt is not null
            && order.PaymentWindowExpiresAt > now
            && order.PayOsOrderCode.HasValue
            && !string.IsNullOrWhiteSpace(order.PaymentLinkUrl);
    }

    private static StartOrderAttemptResult ToStartResult(CustomerOrder order)
    {
        return new StartOrderAttemptResult(
            order.Id,
            order.OrderNumber,
            order.Total,
            order.Status.ToString(),
            order.PaymentLinkUrl);
    }

    private static string AppendOrderIdToUrl(string url, Guid orderId)
    {
        if (url.Contains("orderId=", StringComparison.OrdinalIgnoreCase))
            return url;

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}orderId={Uri.EscapeDataString(orderId.ToString())}";
    }

    private static string GenerateOrderNumber()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"XT-{date}-{random}";
    }
}
