using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace Cart.ShoppingCarts.Features.RemoveFromCart;

public record RemoveFromCartRequest(Guid CartId, Guid VariantId);
public record RemoveFromCartResponse(Guid CartId, CartDto? Cart);

public class RemoveFromCartEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/cart/{cartId}/items/{variantId}",
            async ([AsParameters] RemoveFromCartRequest request,
                   ISender sender) =>
        {
            var command = new RemoveFromCartCommand(request.CartId, request.VariantId);

            var result = await sender.Send(command);

            var response = result.Adapt<RemoveFromCartResponse>();

            return Results.Ok(response);
        })
        .Produces<RemoveFromCartResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Remove item from cart");
    }
}
