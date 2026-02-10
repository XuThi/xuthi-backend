namespace ProductCatalog.Features.Products.GetProduct;

public class GetProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Get by ID
        app.MapGet("/api/products/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetProductQuery(Id: id);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetProductById")
        .Produces<ProductDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Product by ID")
        .WithDescription("Get detailed product information by its ID")
        .WithTags("Products");

        // Get by slug (SEO-friendly URLs)
        app.MapGet("/api/products/by-slug/{slug}", async (string slug, ISender sender) =>
        {
            var query = new GetProductQuery(Slug: slug);
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetProductBySlug")
        .Produces<ProductDetailResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Product by Slug")
        .WithDescription("Get detailed product information by its URL slug")
        .WithTags("Products");
    }
}
