using Mapster;

namespace ProductCatalog.Features.Groups.UpdateGroup;

public record UpdateGroupRequest(string? Name = null);
public record UpdateGroupRouteRequest(Guid Id);
public record UpdateGroupResponse(Guid Id, string Name, int ProductCount);

public class UpdateGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/groups/{id:guid}", async (
            [AsParameters] UpdateGroupRouteRequest route,
            UpdateGroupRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateGroupCommand(route.Id, request));
            var response = result.Adapt<UpdateGroupResponse>();
            return Results.Ok(response);
        })
        .WithName("UpdateGroup")
        .Produces<UpdateGroupResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Product Group")
        .WithDescription("Update product group name")
        .WithTags("Product Groups")
        .RequireAuthorization("Staff");
    }
}
