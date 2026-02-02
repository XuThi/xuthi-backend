namespace Order.Features.GetOrders;

public class GetOrdersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/orders", async (
            [AsParameters] GetOrdersQuery query,
            ISender sender) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetOrders")
        .Produces<GetOrdersResult>(StatusCodes.Status200OK)
        .WithSummary("Get Orders")
        .WithDescription("Get all orders with optional filters")
        .WithTags("Orders");
    }
}
