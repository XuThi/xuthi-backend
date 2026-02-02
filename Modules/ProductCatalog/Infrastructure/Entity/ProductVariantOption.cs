namespace ProductCatalog.Infrastructure.Entity;

public class ProductVariantOption
{
    public Guid ProductId { get; set; }
    public string VariantOptionId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
