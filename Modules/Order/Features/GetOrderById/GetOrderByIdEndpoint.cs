using Order.Features.GetOrder;

namespace Order.Features.GetOrderById;

public class GetOrderByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/orders/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetOrderQuery(Id: id);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetOrderById")
        .Produces<OrderDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Order by ID")
        .WithDescription("Get detailed order information")
        .WithTags("Orders");
    }
}