namespace ProductCatalog.Products.Features.Variants;

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

public record OptionSelectionInput(string VariantOptionId, string Value);

public record OptionSelectionResult(string VariantOptionId, string Value);
