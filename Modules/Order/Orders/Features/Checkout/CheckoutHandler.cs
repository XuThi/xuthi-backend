using Cart.ShoppingCarts.Features.QuoteAndConsumeCart;
using Order.Orders.Events;
using Order.Orders.Services;
using ProductCatalog.Products.Services;
using Order.Orders.Features.GetShippingSettings;
using Promotion.Vouchers.Features.RedeemVoucher;

namespace Order.Orders.Features.Checkout;

public record CheckoutCommand(CheckoutRequest Request) : ICommand<CheckoutResult>;
public record CheckoutResult(Guid OrderId, string OrderNumber, decimal Total, string Status, string? PaymentUrl = null);

public class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(x => x.Request.CartId).NotEmpty();
        RuleFor(x => x.Request.CustomerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.CustomerPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.ShippingAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.ShippingCity).NotEmpty();
        RuleFor(x => x.Request.ShippingWard);
        RuleFor(x => x.Request.ReturnUrl)
            .NotEmpty()
            .When(x => x.Request.PaymentMethod == PaymentMethod.PayOS);
        RuleFor(x => x.Request.CancelUrl)
            .NotEmpty()
            .When(x => x.Request.PaymentMethod == PaymentMethod.PayOS);
    }
}

internal class CheckoutHandler(
    OrderDbContext orderDb,
    IStockReservationService stockReservation,
    IPaymentService paymentService,
    ISender sender)
    : ICommandHandler<CheckoutCommand, CheckoutResult>
{
    public async Task<CheckoutResult> Handle(CheckoutCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        var quote = (await sender.Send(new QuoteCartForCheckoutCommand(req.CartId, req.CustomerId), cancellationToken)).Quote;

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

        var subtotal = quote.Subtotal;
        var discountAmount = quote.VoucherDiscount;
        var voucherId = quote.AppliedVoucherId;

        // 2. Calculate shipping based on dynamic settings
        var shippingResult = await sender.Send(new CalculateShipping.CalculateShippingQuery(
            req.PaymentMethod,
            req.ShippingCity,
            req.ShippingWard,
            quote.Items.Select(i => new CalculateShipping.CalculateShippingItem(i.ProductId, i.VariantId, i.Quantity)).ToList(),
            req.ShippingDistrict
        ), cancellationToken);
        decimal shippingFee = shippingResult.ShippingFee;
        if (quote.WaivesShipping)
            shippingFee = 0;

        // 3. Create order
        var orderId = Guid.NewGuid();
        var orderNumber = GenerateOrderNumber();
        var total = subtotal - discountAmount + shippingFee;

        var order = new CustomerOrder
        {
            Id = orderId,
            OrderNumber = orderNumber,
            CustomerId = quote.CustomerId,
            CustomerName = req.CustomerName,
            CustomerEmail = req.CustomerEmail,
            CustomerPhone = req.CustomerPhone,
            ShippingAddress = req.ShippingAddress,
            ShippingCity = req.ShippingCity,
            ShippingWard = !string.IsNullOrWhiteSpace(req.ShippingDistrict)
                ? $"{req.ShippingDistrict}, {req.ShippingWard}"
                : req.ShippingWard,
            ShippingNote = req.ShippingNote,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            ShippingFee = shippingFee,
            Total = total,
            VoucherId = voucherId,
            VoucherCode = quote.AppliedVoucherCode,
            PaymentMethod = req.PaymentMethod,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            Items = orderItems
        };

        // 4. Reserve stock for the same window as the PayOS payment link.
        // This prevents the reservation from expiring while the customer can still pay.
        var sessionKey = $"order:{order.Id}";
        order.ReservationSessionKey = sessionKey;

        await stockReservation.ReserveStockAsync(
            sessionKey,
            quote.Items.Select(i => (i.VariantId, i.Quantity)).ToList(),
            TimeSpan.FromMinutes(5),
            cancellationToken);

        // 5. Save order
        orderDb.Orders.Add(order);
        await orderDb.SaveChangesAsync(cancellationToken);

        try
        {
            if (voucherId.HasValue)
            {
                await sender.Send(new RedeemVoucherCommand(
                    voucherId.Value,
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

        await sender.Send(new ConsumeQuotedCartCommand(req.CartId, req.CustomerId), cancellationToken);

        // COD can send order notifications immediately after voucher redemption succeeds.
        // PayOS notifications are published only after webhook confirms payment success.
        if (req.PaymentMethod == PaymentMethod.CashOnDelivery)
        {
            order.AddDomainEvent(OrderCreatedEventFactory.FromOrder(order));
            await orderDb.SaveChangesAsync(cancellationToken);
        }

        // 6. Handle payment based on method
        string? paymentUrl = null;

        if (req.PaymentMethod == PaymentMethod.PayOS)
        {
            // Create PayOS payment link
            var returnUrl = AppendOrderIdToUrl(
                req.ReturnUrl ?? throw new InvalidOperationException("ReturnUrl is required for PayOS"),
                order.Id);
            var cancelUrl = AppendOrderIdToUrl(
                req.CancelUrl ?? throw new InvalidOperationException("CancelUrl is required for PayOS"),
                order.Id);

            var payResult = await paymentService.CreatePaymentLinkAsync(
                order, returnUrl, cancelUrl, cancellationToken);

            order.PayOsOrderCode = payResult.OrderCode;
            paymentUrl = payResult.CheckoutUrl;

            await orderDb.SaveChangesAsync(cancellationToken);
            
            // Wake up the expired payment cleanup cronjob to start checking for expired links
            BackgroundServices.ExpiredPaymentCleanupService.RequireCheck = true;
        }
        else if (req.PaymentMethod == PaymentMethod.CashOnDelivery)
        {
            // COD: Confirm reservation immediately (stock is committed)
            await stockReservation.ConfirmReservationsAsync(sessionKey, order.Id, cancellationToken);
        }

        return new CheckoutResult(
            order.Id,
            order.OrderNumber,
            order.Total,
            order.Status.ToString(),
            paymentUrl
        );
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
        var random = new Random().Next(1000, 9999);
        return $"XT-{date}-{random}";
    }

}
