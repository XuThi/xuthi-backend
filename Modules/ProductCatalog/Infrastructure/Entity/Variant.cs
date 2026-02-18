namespace ProductCatalog.Infrastructure.Entity;

// TODO: Figuring out why tf do we add stockquantity and compareAtPrice

public class Variant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string BarCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


    public List<VariantOptionSelection> OptionSelections { get; set; } = [];
    public List<VariantImage> Images { get; set; } = [];
}