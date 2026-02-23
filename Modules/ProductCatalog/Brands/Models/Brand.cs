using Core.DDD;

namespace ProductCatalog.Brands.Models;

public class Brand : Entity<Guid>
{
    public string Name { get; set; } = default!;
    public string UrlSlug { get; set; } = default!;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
}
