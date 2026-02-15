namespace ProductCatalog.Features.Groups.GetGroup;

public record GetGroupRequest(Guid Id);
public record GetGroupResponse(GroupDetailResult Group);

public class GetGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/groups/{id:guid}", async (
            [AsParameters] GetGroupRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetGroupQuery(request.Id));
            var response = new GetGroupResponse(result);
            return Results.Ok(response);
        })
        .WithName("GetGroup")
        .Produces<GetGroupResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Product Group")
        .WithDescription("Get product group by ID with all products")
        .WithTags("Product Groups");
    }
}
