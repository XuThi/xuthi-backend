using Mapster;

namespace ProductCatalog.Features.Variants.UpdateVariant;

public record OptionSelectionInput(string VariantOptionId, string Value);

public record UpdateVariantRequest(
    string? Sku = null,
    string? BarCode = null,
    decimal? Price = null,
    string? Description = null,
    bool? IsActive = null,
    List<OptionSelectionInput>? OptionSelections = null
);

public record UpdateVariantRouteRequest(Guid VariantId);

public record UpdateVariantResponse(
    Guid Id,
    Guid ProductId,
    string Sku,
    string BarCode,
    decimal Price,
    string Description,
    bool IsActive,
    List<OptionSelectionResult> OptionSelections
);

public class UpdateVariantEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/variants/{variantId:guid}", async (
            [AsParameters] UpdateVariantRouteRequest route,
            UpdateVariantRequest request,
            ISender sender) =>
        {
            var command = new UpdateVariantCommand(route.VariantId, request);
            var result = await sender.Send(command);
            var response = result.Adapt<UpdateVariantResponse>();
            return Results.Ok(response);
        })
        .WithName("UpdateVariant")
        .Produces<UpdateVariantResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Variant")
        .WithDescription("Update an existing variant")
        .WithTags("Variants");
    }
}
