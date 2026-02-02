using System.Text.Json.Serialization;

namespace ProductCatalog.Infrastructure.Entity;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UrlSlug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid BrandId { get; set; }
    public Guid CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Brand Brand { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public List<Variant> Variants { get; set; } = [];
    public List<ProductImage> Images { get; set; } = [];
    public List<ProductVariantOption> VariantOptions { get; set; } = [];
}