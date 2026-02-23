namespace Cart.ShoppingCarts.Features.MergeCarts;

public record MergeCartsRequest(string SessionId, Guid CustomerId);
public record MergeCartsResponse(bool Success, CartDto? Cart);

public class MergeCartsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/cart/merge", async (MergeCartsRequest request, ISender sender) =>
        {
            var command = new MergeCartsCommand(request.SessionId, request.CustomerId);

            var result = await sender.Send(command);

            var response = new MergeCartsResponse(result.Success, result.Cart);

            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<MergeCartsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Merge anonymous cart to customer")
        .WithDescription("After login, merge anonymous session cart into customer cart");
    }
}
