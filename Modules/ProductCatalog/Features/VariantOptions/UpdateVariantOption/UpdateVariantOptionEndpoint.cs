using ProductCatalog.Features.VariantOptions.GetVariantOptions;

namespace ProductCatalog.Features.VariantOptions.UpdateVariantOption;

public class UpdateVariantOptionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/variant-options/{id}", async (string id, UpdateVariantOptionRequest request, ISender sender) =>
        {
            var command = new UpdateVariantOptionCommand(id, request);
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateVariantOption")
        .Produces<VariantOptionResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Variant Option")
        .WithDescription("Update a variant option definition")
        .WithTags("VariantOptions")
        .RequireAuthorization("Staff");
    }
}
