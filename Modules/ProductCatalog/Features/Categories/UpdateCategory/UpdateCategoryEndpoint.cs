namespace ProductCatalog.Features.Categories.UpdateCategory;

public record UpdateCategoryRequest(
    string? Name,
    string? UrlSlug,
    string? Description,
    Guid? ParentCategoryId,
    int? SortOrder
);

public record UpdateCategoryRouteRequest(Guid Id);

public record UpdateCategoryResponse(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid? ParentCategoryId,
    int SortOrder
);

public class UpdateCategoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/categories/{id:guid}", async (
            [AsParameters] UpdateCategoryRouteRequest route,
            UpdateCategoryRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateCategoryCommand(route.Id, request));
            var response = new UpdateCategoryResponse(
                result.Id,
                result.Name,
                result.UrlSlug,
                result.Description,
                result.ParentCategoryId,
                result.SortOrder);
            return Results.Ok(response);
        })
        .WithName("UpdateCategory")
        .Produces<UpdateCategoryResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Category")
        .WithDescription("Update category details")
        .WithTags("Categories")
        .RequireAuthorization("Staff");
    }
}
