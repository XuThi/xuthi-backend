namespace ProductCatalog.Features.Groups.GetGroupByName;

public record GetGroupByNameRequest(string Name);
public record GetGroupByNameResponse(GroupDetailResult Group);

public class GetGroupByNameEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/groups/by-name/{name}", async (
            [AsParameters] GetGroupByNameRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetGroupByNameQuery(request.Name));
            var response = new GetGroupByNameResponse(result);
            return Results.Ok(response);
        })
        .WithName("GetGroupByName")
        .Produces<GetGroupByNameResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Product Group by Name")
        .WithDescription("Get product group by exact name")
        .WithTags("Product Groups");
    }
}
