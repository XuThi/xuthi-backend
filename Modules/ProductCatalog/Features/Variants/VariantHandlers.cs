namespace ProductCatalog.Features.Variants;

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

        // Add option selections
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

        // Update option selections if provided
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

internal class DeleteVariantHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteVariantCommand, bool>
{
    public async Task<bool> Handle(DeleteVariantCommand command, CancellationToken cancellationToken)
    {
        var variant = await dbContext.Variants.FindAsync([command.VariantId], cancellationToken);
        if (variant is null)
            throw new KeyNotFoundException("Variant not found");

        variant.IsDeleted = true;
        variant.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
