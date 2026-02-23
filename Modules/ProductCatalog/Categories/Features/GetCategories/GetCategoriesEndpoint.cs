using Mapster;

namespace ProductCatalog.Categories.Features.GetCategories;

public record GetCategoriesRequest(Guid? ParentId = null);
public record GetCategoriesResponse(List<CategoryItem> Categories);

public class GetCategoriesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories", async ([AsParameters] GetCategoriesRequest request, ISender sender) =>
        {
            var query = new GetCategoriesQuery(request.ParentId);
            var result = await sender.Send(query);
            var response = result.Adapt<GetCategoriesResponse>();
            return Results.Ok(response);
        })
        .WithName("GetCategories")
        .Produces<GetCategoriesResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get Categories")
        .WithDescription("Get all categories, optionally filtered by parent category")
        .WithTags("Categories");
    }
}
