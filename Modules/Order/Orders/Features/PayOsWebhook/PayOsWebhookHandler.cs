using System.Text.Json;
using Order.Orders.Events;
using Order.Orders.Services;
using PayOS.Models.Webhooks;
using ProductCatalog.Products.Services;

namespace Order.Orders.Features.PayOsWebhook;

public record PayOsWebhookCommand(string RawPayload) : ICommand<PayOsWebhookResult>;
public record PayOsWebhookResult(bool Accepted = true);

internal class PayOsWebhookHandler(
    OrderDbContext orderDb,
    IPaymentService paymentService,
    IStockReservationService stockReservation)
    : ICommandHandler<PayOsWebhookCommand, PayOsWebhookResult>
{
    public async Task<PayOsWebhookResult> Handle(PayOsWebhookCommand command, CancellationToken cancellationToken)
    {
        var sdkWebhook = JsonSerializer.Deserialize<Webhook>(
            command.RawPayload,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new JsonException("Invalid PayOS webhook payload");

        var result = await paymentService.HandleWebhookAsync(sdkWebhook, cancellationToken);

        // Find order by PayOS order code
        var order = await orderDb.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.PayOsOrderCode == result.OrderCode, cancellationToken);

        if (order is null)
            return new PayOsWebhookResult();

        // Idempotency/terminal-state guards for repeated webhooks.
        if (order.PaymentStatus == PaymentStatus.Paid)
            return new PayOsWebhookResult();

        if (order.Status == OrderStatus.Cancelled)
            return new PayOsWebhookResult();

        if (!result.IsSuccess && order.PaymentStatus == PaymentStatus.Failed)
            return new PayOsWebhookResult();

        if (result.IsSuccess)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt = DateTime.UtcNow;
            order.Status = OrderStatus.Confirmed;

            // Confirm stock reservation — deducts actual stock
            if (!string.IsNullOrEmpty(order.ReservationSessionKey))
            {
                await stockReservation.ConfirmReservationsAsync(
                    order.ReservationSessionKey, order.Id, cancellationToken);
            }

            // Notify customer + owner only after payment is confirmed.
            order.AddDomainEvent(OrderCreatedEventFactory.FromOrder(order));
        }
        else
        {
            order.PaymentStatus = PaymentStatus.Failed;
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = "Thanh toán PayOS thất bại hoặc bị hủy";

            // Release stock reservation
            if (!string.IsNullOrEmpty(order.ReservationSessionKey))
            {
                await stockReservation.ReleaseReservationsAsync(order.ReservationSessionKey, cancellationToken);
            }
        }

        await orderDb.SaveChangesAsync(cancellationToken);

        return new PayOsWebhookResult();
    }
}
