namespace ProductCatalog.Infrastructure.Entity;

public class Category
{
    public Guid Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string UrlSlug { get; set; } = default!;
    public string? Description { get; set; }
    public Guid ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
}