namespace Cart.Features.ClearCart;

public record ClearCartRequest(Guid CartId);
// Response
public record ClearCartResponse(bool Success);

// Endpoint
public class ClearCartEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/cart/{cartId:guid}", async (
            [AsParameters] ClearCartRequest request,
            ISender sender) =>
        {
            var command = new ClearCartCommand(request.CartId);
            
            var result = await sender.Send(command);
            
            var response = new ClearCartResponse(result.Success);

            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<ClearCartResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Clear entire cart")
        .WithDescription("Remove all items and vouchers from cart");
    }
}
