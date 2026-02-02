namespace ProductCatalog.Features.GetBrands;

public class GetBrandsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/brands", async (ISender sender) =>
        {
            var query = new GetBrandsQuery();
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("GetBrands")
        .Produces<GetBrandsResult>(StatusCodes.Status200OK)
        .WithSummary("Get All Brands")
        .WithDescription("Get all brands")
        .WithTags("Brands");
    }
}
