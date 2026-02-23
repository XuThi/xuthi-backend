namespace ProductCatalog.VariantOptions.Models;

public class VariantOptionValue
{
    public Guid Id { get; set; }
    public string VariantOptionId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? DisplayValue { get; set; }
    public int SortOrder { get; set; }
}
