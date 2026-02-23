using Core.DDD;

namespace ProductCatalog.Categories.Models;

public class Category : Entity<Guid>
{
    public string Name { get; set; } = default!;
    public string UrlSlug { get; set; } = default!;
    public string? Description { get; set; }
    public Guid ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
}
