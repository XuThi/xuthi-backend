using Mapster;

namespace Cart.Features.GetCart;

public record GetCartRequest(string? SessionId, Guid? CustomerId, Guid? CartId);
public record GetCartResponse(CartDto? Cart);

public class GetCartEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cart", async ([AsParameters] GetCartRequest request, ISender sender) =>
        {
            var command = request.Adapt<GetCartQuery>();

            var result = await sender.Send(command);

            var response = result.Adapt<GetCartResponse>();

            return Results.Ok(response);
        })
        .Produces<GetCartResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Shopping Cart")
        .WithSummary("Get shopping cart")
        .WithDescription("Get cart by sessionId (anonymous), customerId (logged in), or cartId (direct Guid)");
    }
}
