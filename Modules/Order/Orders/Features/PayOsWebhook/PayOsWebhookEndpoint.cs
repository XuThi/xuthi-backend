using Order.Orders.Services;
using ProductCatalog.Products.Services;

namespace Order.Orders.Features.PayOsWebhook;

public class PayOsWebhookEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/payos/webhook", async (
            HttpRequest httpRequest,
            OrderDbContext orderDb,
            IPaymentService paymentService,
            IStockReservationService stockReservation) =>
        {
            // Read body
            using var reader = new StreamReader(httpRequest.Body);
            var body = await reader.ReadToEndAsync();
            var signature = httpRequest.Headers["x-signature"].FirstOrDefault() ?? "";

            WebhookResult result;
            try
            {
                result = await paymentService.HandleWebhookAsync(body, signature);
            }
            catch
            {
                return Results.BadRequest(new { success = false, message = "Invalid webhook" });
            }

            // Find order by PayOS order code
            var order = await orderDb.Orders
                .FirstOrDefaultAsync(o => o.PayOsOrderCode == result.OrderCode);

            if (order is null)
                return Results.Ok(new { success = true }); // Ack but nothing to do (e.g. test webhook)

            if (result.IsSuccess)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.PaidAt = DateTime.UtcNow;
                order.Status = OrderStatus.Confirmed;

                // Confirm stock reservation — deducts actual stock
                if (!string.IsNullOrEmpty(order.ReservationSessionKey))
                {
                    await stockReservation.ConfirmReservationsAsync(
                        order.ReservationSessionKey, order.Id);
                }
            }
            else
            {
                order.PaymentStatus = PaymentStatus.Failed;

                // Release stock reservation
                if (!string.IsNullOrEmpty(order.ReservationSessionKey))
                {
                    await stockReservation.ReleaseReservationsAsync(order.ReservationSessionKey);
                }
            }

            await orderDb.SaveChangesAsync();

            return Results.Ok(new { success = true });
        })
        .WithName("PayOSWebhook")
        .WithTags("Payments")
        .WithSummary("PayOS webhook receiver")
        .WithDescription("Receives payment notification from PayOS and updates order + stock")
        .AllowAnonymous();
    }
}
