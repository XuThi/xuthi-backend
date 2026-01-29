namespace ProductCatalog.Infrastructure.Dtos;

public class CategoryInfo
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string UrlSlug { get; set; } = default!;
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
}