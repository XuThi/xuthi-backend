namespace Cart.ShoppingCarts.Features.SyncCartPrices;

// Response
public record SyncCartPricesResponse(bool Success, CartDto? Cart, List<string>? Warnings);

// Endpoint
public class SyncCartPricesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/cart/{cartId:guid}/sync", async (Guid cartId, ISender sender) =>
        {
            var command = new SyncCartPricesCommand(cartId);

            var result = await sender.Send(command);

            var response = new SyncCartPricesResponse(result.Success, result.Cart, result.Warnings);

            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<SyncCartPricesResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Sync cart with catalog")
        .WithDescription("Updates prices and stock availability from ProductCatalog. Call before checkout.");
    }
}
