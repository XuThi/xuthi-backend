namespace ProductCatalog.Products.Dtos;

public class VariantInfo
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = default!;
    public string BarCode { get; set; } = default!;
    public decimal Price { get; set; }
    public string Description { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }
    public List<VariantDimensionValueInfo> DimensionValues { get; set; } = [];
}
