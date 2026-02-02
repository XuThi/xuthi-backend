namespace ProductCatalog.Infrastructure.Entity;

public class VariantOptionSelection
{
    public Guid VariantId { get; set; }
    public string VariantOptionId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}