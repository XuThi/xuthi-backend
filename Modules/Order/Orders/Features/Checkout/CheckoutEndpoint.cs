using Mapster;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace Order.Orders.Features.Checkout;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CheckoutRequest(
    Guid CartId,

    // Customer info
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,

    // Shipping
    string ShippingAddress,
    string ShippingCity,
    string ShippingWard,
    string? ShippingNote,

    // Payment
    PaymentMethod PaymentMethod,

    // PayOS redirect URLs (required for PayOS payment)
    string? ReturnUrl = null,
    string? CancelUrl = null,
    string? ShippingDistrict = null
);
public record CheckoutResponse(Guid OrderId, string OrderNumber, decimal Total, string Status, string? PaymentUrl = null);

public class CheckoutEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders/checkout", async (
            CheckoutRequest request,
            ClaimsPrincipal principal,
            ISender sender) =>
        {
            var externalUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(externalUserId))
                return Results.Unauthorized();

            var command = new CheckoutCommand(request, externalUserId);

            var result = await sender.Send(command);

            var response = result.Adapt<CheckoutResponse>();

            return Results.Created($"/api/orders/{result.OrderId}", response);
        })
        .WithName("Checkout")
        .Produces<CheckoutResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .RequireAuthorization("Authenticated")
        .WithSummary("Checkout")
        .WithDescription("Create a new order from cart")
        .WithTags("Orders");
    }
}
