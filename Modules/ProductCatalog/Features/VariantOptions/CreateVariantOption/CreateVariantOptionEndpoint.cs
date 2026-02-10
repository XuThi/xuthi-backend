using ProductCatalog.Features.VariantOptions.GetVariantOptions;

namespace ProductCatalog.Features.VariantOptions.CreateVariantOption;

public class CreateVariantOptionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/variant-options", async (CreateVariantOptionRequest request, ISender sender) =>
        {
            var command = new CreateVariantOptionCommand(request);
            var result = await sender.Send(command);
            return Results.Created($"/api/variant-options/{result.Id}", result);
        })
        .WithName("CreateVariantOption")
        .Produces<VariantOptionResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .WithSummary("Create Variant Option")
        .WithDescription("Create a new variant option definition")
        .WithTags("VariantOptions")
        .RequireAuthorization("Staff");
    }
}
