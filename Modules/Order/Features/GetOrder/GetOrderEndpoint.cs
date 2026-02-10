namespace Order.Features.GetOrder;

public class GetOrderEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/orders/by-number/{orderNumber}", async (string orderNumber, ISender sender) =>
        {
            var query = new GetOrderQuery(OrderNumber: orderNumber);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetOrderByNumber")
        .Produces<OrderDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Order by Number")
        .WithDescription("Get order by order number (e.g., XT-20260131-1234)")
        .WithTags("Orders");
    }
}
