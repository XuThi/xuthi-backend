namespace ProductCatalog.Features.Categories.GetCategories;

public class GetCategoriesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", async ([AsParameters] Guid parentId, ISender sender) =>
        {
            var query = new GetCategoriesQuery(parentId);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetCategories")
        .Produces<GetCategoriesResult>(StatusCodes.Status200OK)
        .WithSummary("Get Categories")
        .WithDescription("Get all categories, optionally filtered by parent category")
        .WithTags("Categories");
    }
}
