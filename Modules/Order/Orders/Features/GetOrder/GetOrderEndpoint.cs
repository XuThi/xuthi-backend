using Mapster;
using Order.Orders.Features.GetOrder;

namespace Order.Orders.Features.GetOrder;

public record GetOrderRequest(string OrderNumber);
public record GetOrderResponse(OrderDetailResult Order);

public class GetOrderEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/orders/by-number/{orderNumber}", async (
            [AsParameters] GetOrderRequest request,
            ISender sender) =>
        {
            var query = new GetOrderQuery(OrderNumber: request.OrderNumber);
            var result = await sender.Send(query);
            return Results.Ok(new GetOrderResponse(result));
        })
        .WithName("GetOrderByNumber")
        .Produces<GetOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get Order by Number")
        .WithDescription("Get order by order number (e.g., XT-20260131-1234)")
        .WithTags("Orders");
    }
}
