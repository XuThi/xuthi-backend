namespace ProductCatalog.Features.GetCategories;

public record GetCategoriesQuery(Guid? ParentId = null) : IQuery<GetCategoriesResult>;

public record GetCategoriesResult(List<CategoryItem> Categories);

public record CategoryItem(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid ParentCategoryId,
    int SortOrder,
    int ProductCount
);
