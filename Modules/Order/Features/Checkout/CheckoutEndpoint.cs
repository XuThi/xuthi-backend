using Mapster;

namespace Order.Features.Checkout;

public record CheckoutRequest(
    Guid? CustomerId,

    // Customer info
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,

    // Shipping
    string ShippingAddress,
    string ShippingCity,
    string ShippingDistrict,
    string ShippingWard,
    string? ShippingNote,

    // Payment
    PaymentMethod PaymentMethod,

    // Cart items
    List<CheckoutItem> Items,

    // Optional voucher
    string? VoucherCode
);
public record CheckoutResponse(Guid OrderId, string OrderNumber, decimal Total, string Status);

public class CheckoutEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders/checkout", async (CheckoutRequest request, ISender sender) =>
        {
            var command = new CheckoutCommand(request);

            var result = await sender.Send(command);

            var response = result.Adapt<CheckoutResponse>();

            return Results.Created($"/api/orders/{result.OrderId}", response);
        })
        .WithName("Checkout")
        .Produces<CheckoutResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Checkout")
        .WithDescription("Create a new order from cart")
        .WithTags("Orders");
    }
}
