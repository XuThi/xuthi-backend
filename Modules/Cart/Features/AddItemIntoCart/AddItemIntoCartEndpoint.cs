
using Mapster;

namespace Cart.Features.AddItemIntoCart;

public record AddToCartRequest(string? SessionId, Guid? CustomerId, Guid ProductId, Guid VariantId, int Quantity = 1);
public record AddToCartResponse(Guid CartId, CartDto Cart);

public class AddItemIntoCartEndpoint : ICarterModule
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
        .WithTags("Shopping Cart")
        .WithSummary("Add item to cart")
        .WithDescription("Add product variant to cart. Creates cart if doesn't exist.");
    }
}