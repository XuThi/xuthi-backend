namespace ProductCatalog.Features.Variants.GetProductVariants;

public record GetProductVariantsQuery(Guid ProductId) : IQuery<List<VariantResult>>;

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

internal class GetProductVariantsHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetProductVariantsQuery, List<VariantResult>>
{
    public async Task<List<VariantResult>> Handle(GetProductVariantsQuery query, CancellationToken cancellationToken)
    {
        return await dbContext.Variants
            .Where(v => v.ProductId == query.ProductId && !v.IsDeleted)
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
            .ToListAsync(cancellationToken);
    }
}
