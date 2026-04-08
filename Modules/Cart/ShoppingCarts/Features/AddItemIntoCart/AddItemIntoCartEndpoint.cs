using Mapster;

namespace Cart.ShoppingCarts.Features.AddItemIntoCart;

// Request and Response
public record AddToCartRequest(string? SessionId, Guid? CustomerId, Guid ProductId, Guid VariantId, int Quantity = 1);
public record AddToCartResponse(Guid CartId, CartDto Cart);

// Endpoint
public class AddToCartEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/cart/items", async (AddToCartRequest request, ISender sender) =>
        {
            var command = request.Adapt<AddToCartCommand>();

            var result = await sender.Send(command);

            var response = result.Adapt<AddToCartResponse>();

            return Results.Ok(response);
        })
        .Produces<AddToCartResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Shopping Cart")
        .WithSummary("Add item to cart")
        .WithDescription("Add a product variant to the cart. Creates cart if doesn't exist.");
    }
}
