namespace ProductCatalog.Features.VariantOptions.GetVariantOptions;

public class GetVariantOptionsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/variant-options", async (ISender sender) =>
        {
            var result = await sender.Send(new GetVariantOptionsQuery());
            return Results.Ok(result);
        })
        .WithName("GetVariantOptions")
        .Produces<List<VariantOptionResult>>(StatusCodes.Status200OK)
        .WithSummary("Get All Variant Options")
        .WithDescription("Get all available variant options (e.g. Size, Color) and their values")
        .WithTags("VariantOptions");
    }
}
