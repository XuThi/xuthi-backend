namespace ProductCatalog.Products.Features.Variants.UpdateVariant;

public record UpdateVariantCommand(Guid VariantId, UpdateVariantRequest Input) : ICommand<VariantResult>;

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

internal class UpdateVariantHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateVariantCommand, VariantResult>
{
    public async Task<VariantResult> Handle(UpdateVariantCommand command, CancellationToken cancellationToken)
    {
        var variant = await dbContext.Variants
            .Include(v => v.OptionSelections)
            .FirstOrDefaultAsync(v => v.Id == command.VariantId && !v.IsDeleted, cancellationToken);

        if (variant is null)
            throw new KeyNotFoundException("Variant not found");

        var input = command.Input;

        if (input.Sku != null) variant.Sku = input.Sku;
        if (input.BarCode != null) variant.BarCode = input.BarCode;
        if (input.Price.HasValue) variant.Price = input.Price.Value;
        if (input.Description != null) variant.Description = input.Description;
        if (input.IsActive.HasValue) variant.IsActive = input.IsActive.Value;

        if (input.OptionSelections != null)
        {
            variant.OptionSelections.Clear();
            foreach (var os in input.OptionSelections)
            {
                variant.OptionSelections.Add(new VariantOptionSelection
                {
                    VariantId = variant.Id,
                    VariantOptionId = os.VariantOptionId,
                    Value = os.Value
                });
            }
        }

        variant.UpdatedAt = DateTime.UtcNow;
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
