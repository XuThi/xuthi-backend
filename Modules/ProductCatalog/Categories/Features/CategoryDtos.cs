namespace ProductCatalog.Categories.Features;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid ParentCategoryId,
    int SortOrder
);
