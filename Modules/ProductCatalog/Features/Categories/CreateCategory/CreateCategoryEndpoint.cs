namespace ProductCatalog.Features.Categories.CreateCategory;

public record CreateCategoryRequest(
    string Name,
    string? Description,
    Guid? ParentCategoryId,
    int SortOrder = 0
);

public record CreateCategoryResponse(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid ParentCategoryId,
    int SortOrder
);

public class CreateCategoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories", async (CreateCategoryRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateCategoryCommand(request));
            var response = new CreateCategoryResponse(
                result.Id,
                result.Name,
                result.UrlSlug,
                result.Description,
                result.ParentCategoryId,
                result.SortOrder);
            return Results.Created($"/api/categories/{result.Id}", response);
        })
        .WithName("CreateCategory")
        .Produces<CreateCategoryResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Category")
        .WithDescription("Create a new product category")
        .WithTags("Categories")
        .RequireAuthorization("Staff");
    }
}
