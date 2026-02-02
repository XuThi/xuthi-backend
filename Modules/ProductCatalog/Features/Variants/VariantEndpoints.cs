namespace ProductCatalog.Features.Variants;

public class VariantEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Create variant for a product
        app.MapPost("/api/products/{productId:guid}/variants", async (
            Guid productId,
            CreateVariantInput request,
            ISender sender) =>
        {
            var command = new CreateVariantCommand(productId, request);
            var result = await sender.Send(command);
            return Results.Created($"/api/variants/{result.Id}", result);
        })
        .WithName("CreateVariant")
        .Produces<VariantResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Variant")
        .WithDescription("Add a new variant to a product")
        .WithTags("Variants");

        // Update variant
        app.MapPut("/api/variants/{variantId:guid}", async (
            Guid variantId,
            UpdateVariantInput request,
            ISender sender) =>
        {
            var command = new UpdateVariantCommand(variantId, request);
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateVariant")
        .Produces<VariantResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Variant")
        .WithDescription("Update an existing variant")
        .WithTags("Variants");

        // Delete variant (soft delete)
        app.MapDelete("/api/variants/{variantId:guid}", async (
            Guid variantId,
            ISender sender) =>
        {
            var result = await sender.Send(new DeleteVariantCommand(variantId));
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteVariant")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Variant")
        .WithDescription("Soft delete a variant")
        .WithTags("Variants");

        // Get variants for a product
        app.MapGet("/api/products/{productId:guid}/variants", async (
            Guid productId,
            ProductCatalogDbContext db) =>
        {
            var variants = await db.Variants
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .Include(v => v.OptionSelections)
                .Select(v => new VariantResult(
                    v.Id,
                    v.ProductId,
                    v.Sku,
                    v.BarCode,
                    v.Price,
                    v.Description,
                    v.IsActive,
                    v.OptionSelections.Select(os => new OptionSelectionResult(os.VariantOptionId, os.Value)).ToList()
                ))
                .ToListAsync();

            return Results.Ok(variants);
        })
        .WithName("GetProductVariants")
        .Produces<List<VariantResult>>(StatusCodes.Status200OK)
        .WithSummary("Get Product Variants")
        .WithDescription("Get all variants for a product")
        .WithTags("Variants");
    }
}
