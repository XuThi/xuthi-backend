namespace ProductCatalog.Groups.Features.GetGroups;

public record GetGroupsRequest(int Page = 1, int PageSize = 20);
public record GetGroupsResponse(GroupsResult Groups);

public class GetGroupsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/groups", async (
            [AsParameters] GetGroupsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetGroupsQuery(request.Page, request.PageSize));
            var response = new GetGroupsResponse(result);
            return Results.Ok(response);
        })
        .WithName("GetGroups")
        .Produces<GetGroupsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get Product Groups")
        .WithDescription("Get paginated list of product groups")
        .WithTags("Product Groups");
    }
}
