using Cart.ShoppingCarts.Features.QuoteAndConsumeCart;
using Microsoft.Extensions.DependencyInjection;
using Order.Orders.Events;
using Order.Orders.Features.CalculateShipping;
using Order.Orders.Services;
using ProductCatalog.Products.Services;
using Promotion.Vouchers.Features.RedeemVoucher;

namespace Order.Orders.OrderIntake;

public interface IOrderIntake
{
    Task<StartOrderAttemptResult> StartOrderAttemptAsync(
        StartOrderAttempt request,
        CancellationToken cancellationToken = default);
}

public record StartOrderAttempt(
    Guid CartId,
    Guid? CustomerId,
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

public static class OrderIntakeServiceCollectionExtensions
{
    public static IServiceCollection AddOrderIntake(this IServiceCollection services)
    {
        return services.AddScoped<IOrderIntake, OrderIntake>();
    }
}

internal class OrderIntake(
    OrderDbContext orderDb,
    IStockReservationService stockReservation,
    IPaymentService paymentService,
    ISender sender)
    : IOrderIntake
{
    public async Task<StartOrderAttemptResult> StartOrderAttemptAsync(
        StartOrderAttempt request,
        CancellationToken cancellationToken = default)
    {
        var quote = (await sender.Send(
            new QuoteCartForCheckoutCommand(request.CartId, request.CustomerId),
            cancellationToken)).Quote;

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

        var order = new CustomerOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(),
            SourceCartId = request.CartId,
            CustomerId = quote.CustomerId,
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

        var sessionKey = $"order:{order.Id}";
        order.ReservationSessionKey = sessionKey;

        await stockReservation.ReserveStockAsync(
            sessionKey,
            quote.Items.Select(i => (i.VariantId, i.Quantity)).ToList(),
            TimeSpan.FromMinutes(5),
            cancellationToken);

        orderDb.Orders.Add(order);
        await orderDb.SaveChangesAsync(cancellationToken);

        try
        {
            if (order.VoucherId.HasValue)
            {
                await sender.Send(new RedeemVoucherCommand(
                    order.VoucherId.Value,
                    quote.CustomerId,
                    order.Id,
                    discountAmount), cancellationToken);
            }
        }
        catch
        {
            orderDb.Orders.Remove(order);
            await orderDb.SaveChangesAsync(cancellationToken);
            await stockReservation.ReleaseReservationsAsync(sessionKey, cancellationToken);
            throw;
        }

        await sender.Send(new ConsumeQuotedCartCommand(request.CartId, request.CustomerId), cancellationToken);

        if (request.PaymentMethod is PaymentMethod.CashOnDelivery or PaymentMethod.BankTransfer)
        {
            await stockReservation.ConfirmReservationsAsync(sessionKey, order.Id, cancellationToken);
            order.CreatedOrderAt ??= DateTime.UtcNow;
            order.AddDomainEvent(OrderCreatedEventFactory.FromOrder(order));
            await orderDb.SaveChangesAsync(cancellationToken);
        }

        string? paymentUrl = null;

        if (request.PaymentMethod == PaymentMethod.PayOS)
        {
            var returnUrl = AppendOrderIdToUrl(
                request.ReturnUrl ?? throw new InvalidOperationException("ReturnUrl is required for PayOS"),
                order.Id);
            var cancelUrl = AppendOrderIdToUrl(
                request.CancelUrl ?? throw new InvalidOperationException("CancelUrl is required for PayOS"),
                order.Id);

            var payResult = await paymentService.CreatePaymentLinkAsync(
                order, returnUrl, cancelUrl, cancellationToken);

            order.PayOsOrderCode = payResult.OrderCode;
            paymentUrl = payResult.CheckoutUrl;

            await orderDb.SaveChangesAsync(cancellationToken);

            BackgroundServices.ExpiredPaymentCleanupService.RequireCheck = true;
        }

        return new StartOrderAttemptResult(
            order.Id,
            order.OrderNumber,
            order.Total,
            order.Status.ToString(),
            paymentUrl);
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
