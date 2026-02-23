using Mapster;

namespace ProductCatalog.Products.Features.SearchProducts;

public record SearchProductsResponse(List<ProductSearchItem> Products, int TotalCount, int Page, int PageSize, int TotalPages);

public class SearchProductsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", async (
            [AsParameters] SearchProductsRequest request,
            ISender sender) =>
        {
            var query = new SearchProductsQuery(request);
            var result = await sender.Send(query);
            var response = result.Adapt<SearchProductsResponse>();
            return Results.Ok(response);
        })
        .WithName("SearchProducts")
        .Produces<SearchProductsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Search Products")
        .WithDescription("Search and filter products with pagination")
        .WithTags("Products");
    }
}
