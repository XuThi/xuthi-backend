namespace ProductCatalog.Groups.Features.AddProductsToGroup;

public record AddProductsToGroupRequest(List<Guid> ProductIds);
public record AddProductsToGroupRouteRequest(Guid GroupId);
public record AddProductsToGroupResponse(GroupDetailResult Group);

public class AddProductsToGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/groups/{groupId:guid}/products", async (
            [AsParameters] AddProductsToGroupRouteRequest route,
            AddProductsToGroupRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new AddProductsToGroupCommand(route.GroupId, request.ProductIds));
            var response = new AddProductsToGroupResponse(result);
            return Results.Ok(response);
        })
        .WithName("AddProductsToGroup")
        .Produces<AddProductsToGroupResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Add Products to Group")
        .WithDescription("Add one or more products to a group")
        .WithTags("Product Groups")
        .RequireAuthorization("Staff");
    }
}
