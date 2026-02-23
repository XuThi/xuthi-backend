using Mapster;

namespace Order.Orders.Features.GetOrders;

public record GetOrdersRequest(
    string? Email = null,
    OrderStatus? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20
);
public record GetOrdersResponse(
    List<OrderSummary> Orders,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public class GetOrdersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/orders", async (
            [AsParameters] GetOrdersRequest request,
            ISender sender) =>
        {
            var query = request.Adapt<GetOrdersQuery>();

            var result = await sender.Send(query);

            var response = result.Adapt<GetOrdersResponse>();

            return Results.Ok(response);
        })
        .Produces<GetOrdersResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithName("GetOrders")
        .WithSummary("Get Orders")
        .WithDescription("Get all orders with optional filters")
        .WithTags("Orders");
    }
}
