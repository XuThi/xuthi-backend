namespace Order.Orders.Features.PayOsWebhook;

public class PayOsWebhookEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/payos/webhook", async (
            HttpRequest httpRequest,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(httpRequest.Body);
            var rawPayload = await reader.ReadToEndAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(rawPayload))
                return Results.BadRequest("Invalid webhook");

            await sender.Send(new PayOsWebhookCommand(rawPayload), cancellationToken);
            return Results.Ok("OK");
        })
        .WithName("PayOSWebhook")
        .WithTags("Payments")
        .WithSummary("PayOS webhook receiver")
        .WithDescription("Receives payment notification from PayOS and updates order + stock")
        .AllowAnonymous();
    }
}
