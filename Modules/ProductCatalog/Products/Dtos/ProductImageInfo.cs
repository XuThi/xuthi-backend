namespace ProductCatalog.Products.Dtos;

public class ProductImageInfo
{
    public Guid ImageId { get; set; }
    public Guid ProductId { get; set; }
    public string ImageUrl { get; set; } = default!;
    public string AltText { get; set; } = default!;
    public int SortOrder { get; set; }
}
