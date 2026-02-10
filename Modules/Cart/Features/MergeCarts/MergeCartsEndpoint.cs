namespace Cart.Features.MergeCarts;

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
            
            return result.Success ? Results.Ok(result.Cart) : Results.NotFound();
        })
        .WithTags("Shopping Cart")
        .WithSummary("Merge anonymous cart to customer")
        .WithDescription("After login, merge anonymous session cart into customer cart");
    }
}
