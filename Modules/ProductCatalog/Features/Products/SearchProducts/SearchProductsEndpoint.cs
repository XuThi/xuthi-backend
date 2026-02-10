namespace ProductCatalog.Features.Products.SearchProducts;

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
            return Results.Ok(result);
        })
        .WithName("SearchProducts")
        .Produces<SearchProductsResult>(StatusCodes.Status200OK)
        .WithSummary("Search Products")
        .WithDescription("Search and filter products with pagination")
        .WithTags("Products");
    }
}
