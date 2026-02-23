using Mapster;

namespace ProductCatalog.Products.Features.Variants.CreateVariant;

public record OptionSelectionInput(string VariantOptionId, string Value);

public record CreateVariantRequest(
    string Sku,
    string? BarCode,
    decimal Price,
    string Description,
    bool IsActive = true,
    List<OptionSelectionInput>? OptionSelections = null
)
{
    public string BarCode { get; init; } = BarCode ?? Sku;
}

public record CreateVariantResponse(
    Guid Id,
    Guid ProductId,
    string Sku,
    string BarCode,
    decimal Price,
    string Description,
    bool IsActive,
    List<OptionSelectionResult> OptionSelections
);

public class CreateVariantEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products/{productId:guid}/variants", async (
            Guid productId,
            CreateVariantRequest request,
            ISender sender) =>
        {
            var command = new CreateVariantCommand(productId, request);
            var result = await sender.Send(command);
            var response = result.Adapt<CreateVariantResponse>();
            return Results.Created($"/api/variants/{result.Id}", response);
        })
        .WithName("CreateVariant")
        .Produces<CreateVariantResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Create Variant")
        .WithDescription("Add a new variant to a product")
        .WithTags("Variants");
    }
}
