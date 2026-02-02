namespace ProductCatalog.Features.Categories;

// ============== CREATE ==============
public record CreateCategoryCommand(CreateCategoryRequest Request) : ICommand<CategoryResult>;

public record CreateCategoryRequest(
    string Name,
    string? Description,
    Guid? ParentCategoryId,
    int SortOrder = 0
);

// ============== UPDATE ==============
public record UpdateCategoryCommand(Guid Id, UpdateCategoryRequest Request) : ICommand<CategoryResult>;

public record UpdateCategoryRequest(
    string? Name,
    string? Description,
    Guid? ParentCategoryId,
    int? SortOrder
);

// ============== DELETE ==============
public record DeleteCategoryCommand(Guid Id) : ICommand<bool>;

// ============== RESULT ==============
public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid ParentCategoryId,
    int SortOrder
);
