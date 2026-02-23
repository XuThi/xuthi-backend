using Mapster;

namespace ProductCatalog.Brands.Features.GetBrands;
public record GetBrandsResponse(List<BrandItem> Brands);


public class GetBrandsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/brands", async (ISender sender) =>
        {
            var query = new GetBrandsQuery();
            var result = await sender.Send(query);
            var response = result.Adapt<GetBrandsResponse>();
            return Results.Ok(response);
        })
        .WithName("GetBrands")
        .Produces<GetBrandsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get All Brands")
        .WithDescription("Get all brands")
        .WithTags("Brands");
    }
}
