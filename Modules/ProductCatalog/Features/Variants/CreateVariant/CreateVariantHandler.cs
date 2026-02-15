namespace ProductCatalog.Features.Variants.CreateVariant;

public record CreateVariantCommand(Guid ProductId, CreateVariantRequest Input) : ICommand<VariantResult>;

public record VariantResult(
    Guid Id,
    Guid ProductId,
    string Sku,
    string BarCode,
    decimal Price,
    string Description,
    bool IsActive,
    List<OptionSelectionResult> OptionSelections
);

public record OptionSelectionResult(string VariantOptionId, string Value);

internal class CreateVariantHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateVariantCommand, VariantResult>
{
    public async Task<VariantResult> Handle(CreateVariantCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FindAsync([command.ProductId], cancellationToken);
        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var input = command.Input;
        var variantId = Guid.NewGuid();

        var variant = new Variant
        {
            Id = variantId,
            ProductId = command.ProductId,
            Sku = input.Sku,
            BarCode = input.BarCode,
            Price = input.Price,
            Description = input.Description,
            IsActive = input.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (input.OptionSelections is { Count: > 0 })
        {
            foreach (var os in input.OptionSelections)
            {
                variant.OptionSelections.Add(new VariantOptionSelection
                {
                    VariantId = variantId,
                    VariantOptionId = os.VariantOptionId,
                    Value = os.Value
                });
            }
        }

        dbContext.Variants.Add(variant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(variant);
    }

    private static VariantResult MapToResult(Variant v) => new(
        v.Id,
        v.ProductId,
        v.Sku,
        v.BarCode,
        v.Price,
        v.Description,
        v.IsActive,
        v.OptionSelections.Select(os => new OptionSelectionResult(os.VariantOptionId, os.Value)).ToList()
    );
}
