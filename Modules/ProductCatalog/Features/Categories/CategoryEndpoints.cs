namespace ProductCatalog.Features.Categories;

public class CategoryEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories", async (CreateCategoryRequest request, ISender sender) =>
        {
            var command = new CreateCategoryCommand(request);
            var result = await sender.Send(command);
            return Results.Created($"/api/categories/{result.Id}", result);
        })
        .WithName("CreateCategory")
        .Produces<CategoryResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Category")
        .WithDescription("Create a new product category")
        .WithTags("Categories")
        .RequireAuthorization("Staff");

        app.MapPut("/api/categories/{id:guid}", async (Guid id, UpdateCategoryRequest request, ISender sender) =>
        {
            var command = new UpdateCategoryCommand(id, request);
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateCategory")
        .Produces<CategoryResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Category")
        .WithDescription("Update category details")
        .WithTags("Categories")
        .RequireAuthorization("Staff");

        app.MapDelete("/api/categories/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteCategoryCommand(id);
            await sender.Send(command);
            return Results.NoContent();
        })
        .WithName("DeleteCategory")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Category")
        .WithDescription("Delete a category (must have no products)")
        .WithTags("Categories")
        .RequireAuthorization("Admin");
    }
}
