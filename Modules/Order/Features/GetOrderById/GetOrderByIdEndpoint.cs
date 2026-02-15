using Mapster;
using Order.Features.GetOrder;

namespace Order.Features.GetOrderById;

public record GetOrderByIdRequest(Guid Id);
public record GetOrderByIdResponse(OrderDetailResult Order);

public class GetOrderByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/orders/{id:guid}", async (
            [AsParameters] GetOrderByIdRequest request,
            ISender sender) =>
        {
            var query = new GetOrderQuery(Id: request.Id);
            var result = await sender.Send(query);
            return Results.Ok(new GetOrderByIdResponse(result));
        })
        .WithName("GetOrderById")
        .Produces<GetOrderByIdResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get Order by ID")
        .WithDescription("Get detailed order information")
        .WithTags("Orders");
    }
}