using Core.DDD;

namespace ProductCatalog.Products.Models;

public class Image : Entity<Guid>
{
    public string Url { get; set; } = default!;
    public string? CloudinaryPublicId { get; set; }
    public string? AltText { get; set; }
}
