namespace ProductCatalog.Features.Variants;

// ============== CREATE ==============
public record CreateVariantCommand(Guid ProductId, CreateVariantInput Input) : ICommand<VariantResult>;

public record CreateVariantInput(
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

public record OptionSelectionInput(string VariantOptionId, string Value);

// ============== UPDATE ==============
public record UpdateVariantCommand(Guid VariantId, UpdateVariantInput Input) : ICommand<VariantResult>;

public record UpdateVariantInput(
    string? Sku = null,
    string? BarCode = null,
    decimal? Price = null,
    string? Description = null,
    bool? IsActive = null,
    List<OptionSelectionInput>? OptionSelections = null
);

// ============== DELETE ==============
public record DeleteVariantCommand(Guid VariantId) : ICommand<bool>;

// ============== RESULT ==============
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
