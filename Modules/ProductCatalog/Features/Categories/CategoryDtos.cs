namespace ProductCatalog.Features.Categories;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid ParentCategoryId,
    int SortOrder
);
