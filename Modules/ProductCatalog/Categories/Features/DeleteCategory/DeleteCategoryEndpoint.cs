namespace ProductCatalog.Categories.Features.DeleteCategory;

public record DeleteCategoryRouteRequest(Guid Id);
public record DeleteCategoryResponse(bool Success);

public class DeleteCategoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/categories/{id:guid}", async (
            [AsParameters] DeleteCategoryRouteRequest route,
            ISender sender) =>
        {
            var result = await sender.Send(new DeleteCategoryCommand(route.Id));
            var response = new DeleteCategoryResponse(result);
            return result ? Results.Ok(response) : Results.NotFound();
        })
        .WithName("DeleteCategory")
        .Produces<DeleteCategoryResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Category")
        .WithDescription("Delete a category (must have no products)")
        .WithTags("Categories")
        .RequireAuthorization("Admin");
    }
}
