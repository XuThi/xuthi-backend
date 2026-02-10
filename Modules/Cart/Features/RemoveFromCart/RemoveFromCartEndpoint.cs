
using Microsoft.AspNetCore.Mvc;

namespace Cart.Features.RemoveFromCart;

//public record RemoveFromCartRequest(Guid cartId, Guid variantId);
public record RemoveFromCartResponse(Guid CartId, CartDto? Cart);

public class RemoveFromCartEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/cart/{cartId}/items/{variantId}",
            async ([FromRoute] Guid cartId,
                   [FromRoute] Guid variantId,
                   ISender sender) =>
        {
            var command = new RemoveFromCartCommand(cartId, variantId);

            var result = await sender.Send(command);

            var response = result.Adapt<RemoveFromCartResponse>();

            return Results.Ok(response);
        })
        .Produces<RemoveFromCartResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Remove item from cart");
    }
}
