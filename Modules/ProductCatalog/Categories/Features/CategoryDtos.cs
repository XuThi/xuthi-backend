namespace ProductCatalog.Categories.Features;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? ImageUrl,
    Guid ParentCategoryId,
    int SortOrder
);
