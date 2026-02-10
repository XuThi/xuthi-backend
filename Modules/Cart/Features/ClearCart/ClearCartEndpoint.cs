namespace Cart.Features.ClearCart;

// Response
public record ClearCartResponse(bool Success);

// Endpoint
public class ClearCartEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/cart/{cartId:guid}", async (Guid cartId, ISender sender) =>
        {
            var command = new ClearCartCommand(cartId);
            
            var result = await sender.Send(command);
            
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Shopping Cart")
        .WithSummary("Clear entire cart")
        .WithDescription("Remove all items and vouchers from cart");
    }
}
