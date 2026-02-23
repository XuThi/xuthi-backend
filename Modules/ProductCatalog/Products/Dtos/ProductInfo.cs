namespace ProductCatalog.Products.Dtos;

public class ProductInfo
{
    public string Name { get; set; } = default!;
    public string UrlSlug { get; set; } = default!;
    public string Description { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; }
    public List<CategoryInfo> Path { get; set; } = default!;
    public BrandInfo Brand { get; set; } = default!;
    public List<VariantInfo> Variants { get; set; } = [];
    public List<DimensionInfo> Dimensions { get; set; } = [];
    public List<GroupInfo> Groups { get; set; } = [];
    public List<ProductImageInfo> Images { get; set; } = [];
}
