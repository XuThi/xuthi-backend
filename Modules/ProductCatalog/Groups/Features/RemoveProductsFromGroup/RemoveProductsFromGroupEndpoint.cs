namespace ProductCatalog.Groups.Features.RemoveProductsFromGroup;

public record RemoveProductsFromGroupRequest(List<Guid> ProductIds);
public record RemoveProductsFromGroupRouteRequest(Guid GroupId);
public record RemoveProductsFromGroupResponse(GroupDetailResult Group);

public class RemoveProductsFromGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/groups/{groupId:guid}/products/remove", async (
            [AsParameters] RemoveProductsFromGroupRouteRequest route,
            RemoveProductsFromGroupRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveProductsFromGroupCommand(route.GroupId, request.ProductIds));
            var response = new RemoveProductsFromGroupResponse(result);
            return Results.Ok(response);
        })
        .WithName("RemoveProductsFromGroup")
        .Produces<RemoveProductsFromGroupResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove Products from Group")
        .WithDescription("Remove one or more products from a group")
        .WithTags("Product Groups")
        .RequireAuthorization("Staff");
    }
}
