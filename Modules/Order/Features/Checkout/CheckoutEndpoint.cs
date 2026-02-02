namespace Order.Features.Checkout;

public class CheckoutEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders/checkout", async (CheckoutRequest request, ISender sender) =>
        {
            var command = new CheckoutCommand(request);
            var result = await sender.Send(command);
            return Results.Created($"/api/orders/{result.OrderId}", result);
        })
        .WithName("Checkout")
        .Produces<CheckoutResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Checkout")
        .WithDescription("Create a new order from cart")
        .WithTags("Orders");
    }
}
