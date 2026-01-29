namespace ProductCatalog.Infrastructure.Dtos;

public class BrandInfo
{
    public Guid BrandId { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string LogoUrl { get; set; } = default!;
}