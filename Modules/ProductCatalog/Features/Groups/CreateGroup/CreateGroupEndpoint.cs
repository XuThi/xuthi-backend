using Mapster;

namespace ProductCatalog.Features.Groups.CreateGroup;

public record CreateGroupRequest(
    string Name,
    List<Guid>? ProductIds = null
);

public record CreateGroupResponse(Guid Id, string Name, int ProductCount);

public class CreateGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/groups", async (CreateGroupRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateGroupCommand(request));
            var response = result.Adapt<CreateGroupResponse>();
            return Results.Created($"/api/groups/{result.Id}", response);
        })
        .WithName("CreateGroup")
        .Produces<CreateGroupResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Product Group")
        .WithDescription("Create a new product group (e.g., 'Best Sellers', 'New Arrivals', 'Sale Items')")
        .WithTags("Product Groups")
        .RequireAuthorization("Staff");
    }
}
