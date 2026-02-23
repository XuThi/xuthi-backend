namespace ProductCatalog.Products.Features.Variants.GetProductVariants;

public record GetProductVariantsRequest(Guid ProductId);
public record GetProductVariantsResponse(List<VariantResult> Variants);

public class GetProductVariantsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{productId:guid}/variants", async (
            [AsParameters] GetProductVariantsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetProductVariantsQuery(request.ProductId));
            var response = new GetProductVariantsResponse(result);
            return Results.Ok(response);
        })
        .WithName("GetProductVariants")
        .Produces<GetProductVariantsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get Product Variants")
        .WithDescription("Get all variants for a product")
        .WithTags("Variants");
    }
}
