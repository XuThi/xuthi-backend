namespace Cart.ShoppingCarts.Features.UpdateCartItem;

// Request and Response
public record UpdateCartItemRequest(int Quantity);
public record UpdateCartItemResponse(bool Success, CartDto? Cart, string? ErrorMessage);

// Endpoint
public class UpdateCartItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/cart/{cartId:guid}/items/{variantId:guid}", async (
            Guid cartId,
            Guid variantId,
            UpdateCartItemRequest request,
            ISender sender) =>
        {
            var command = new UpdateCartItemCommand(cartId, variantId, request.Quantity);

            var result = await sender.Send(command);

            var response = new UpdateCartItemResponse(result.Success, result.Cart, result.ErrorMessage);

            return result.Success
                ? Results.Ok(response)
                : Results.BadRequest(response);
        })
        .Produces<UpdateCartItemResponse>(StatusCodes.Status200OK)
        .Produces<UpdateCartItemResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Update item quantity")
        .WithDescription("Update quantity of item in cart. Set to 0 to remove.");
    }
}
