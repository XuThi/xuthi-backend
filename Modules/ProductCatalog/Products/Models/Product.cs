using Core.DDD;

namespace ProductCatalog.Products.Models;

public class Product : Aggregate<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string UrlSlug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid BrandId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public bool IsFeatured { get; set; }
    public Guid CategoryId { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int WeightGrams { get; set; } = 1000;
    public int LengthCm { get; set; } = 28;
    public int WidthCm { get; set; } = 18;
    public int HeightCm { get; set; } = 9;

    public Brand Brand { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public List<Variant> Variants { get; set; } = [];
    public List<ProductImage> Images { get; set; } = [];
    public List<ProductVariantOption> VariantOptions { get; set; } = [];
    public List<ProductReview> Reviews { get; set; } = [];
}
