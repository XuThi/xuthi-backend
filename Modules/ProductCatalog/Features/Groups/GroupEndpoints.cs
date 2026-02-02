namespace ProductCatalog.Features.Groups;

public class GroupEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/groups").WithTags("Product Groups");

        // ========== QUERIES ==========
        group.MapGet("/", async ([AsParameters] GetGroupsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetGroups")
        .Produces<GroupsResult>(StatusCodes.Status200OK)
        .WithSummary("Get Product Groups")
        .WithDescription("Get paginated list of product groups");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetGroupQuery(id));
            return Results.Ok(result);
        })
        .WithName("GetGroup")
        .Produces<GroupDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Product Group")
        .WithDescription("Get product group by ID with all products");

        group.MapGet("/by-name/{name}", async (string name, ISender sender) =>
        {
            var result = await sender.Send(new GetGroupByNameQuery(name));
            return Results.Ok(result);
        })
        .WithName("GetGroupByName")
        .Produces<GroupDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Product Group by Name")
        .WithDescription("Get product group by exact name");

        // ========== COMMANDS ==========
        group.MapPost("/", async (CreateGroupRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateGroupCommand(request));
            return Results.Created($"/api/groups/{result.Id}", result);
        })
        .WithName("CreateGroup")
        .Produces<GroupResult>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .WithSummary("Create Product Group")
        .WithDescription("Create a new product group (e.g., 'Best Sellers', 'New Arrivals', 'Sale Items')")
        .RequireAuthorization("Staff");

        group.MapPut("/{id:guid}", async (Guid id, UpdateGroupRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdateGroupCommand(id, request));
            return Results.Ok(result);
        })
        .WithName("UpdateGroup")
        .Produces<GroupResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Product Group")
        .WithDescription("Update product group name")
        .RequireAuthorization("Staff");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var success = await sender.Send(new DeleteGroupCommand(id));
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteGroup")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Product Group")
        .WithDescription("Delete product group and unlink all products")
        .RequireAuthorization("Admin");

        // ========== PRODUCT ASSOCIATIONS ==========
        group.MapPost("/{groupId:guid}/products", async (
            Guid groupId, 
            AddProductsRequest request, 
            ISender sender) =>
        {
            var result = await sender.Send(new AddProductsToGroupCommand(groupId, request.ProductIds));
            return Results.Ok(result);
        })
        .WithName("AddProductsToGroup")
        .Produces<GroupDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Add Products to Group")
        .WithDescription("Add one or more products to a group")
        .RequireAuthorization("Staff");

        // Using POST instead of DELETE because DELETE doesn't support body in minimal APIs
        group.MapPost("/{groupId:guid}/products/remove", async (
            Guid groupId, 
            RemoveProductsRequest request, 
            ISender sender) =>
        {
            var result = await sender.Send(new RemoveProductsFromGroupCommand(groupId, request.ProductIds));
            return Results.Ok(result);
        })
        .WithName("RemoveProductsFromGroup")
        .Produces<GroupDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove Products from Group")
        .WithDescription("Remove one or more products from a group")
        .RequireAuthorization("Staff");
    }
}

public record AddProductsRequest(List<Guid> ProductIds);
public record RemoveProductsRequest(List<Guid> ProductIds);
