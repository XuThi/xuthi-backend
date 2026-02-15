namespace ProductCatalog.Features.Groups.DeleteGroup;

public record DeleteGroupRouteRequest(Guid Id);
public record DeleteGroupResponse(bool Success);

public class DeleteGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/groups/{id:guid}", async (
            [AsParameters] DeleteGroupRouteRequest route,
            ISender sender) =>
        {
            var success = await sender.Send(new DeleteGroupCommand(route.Id));
            var response = new DeleteGroupResponse(success);
            return success ? Results.Ok(response) : Results.NotFound();
        })
        .WithName("DeleteGroup")
        .Produces<DeleteGroupResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Product Group")
        .WithDescription("Delete product group and unlink all products")
        .WithTags("Product Groups")
        .RequireAuthorization("Admin");
    }
}
