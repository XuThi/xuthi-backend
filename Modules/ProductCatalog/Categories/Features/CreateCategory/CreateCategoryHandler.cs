namespace ProductCatalog.Categories.Features.CreateCategory;

public record CreateCategoryCommand(CreateCategoryRequest Request) : ICommand<CategoryResult>;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid ParentCategoryId,
    int SortOrder
);

internal class CreateCategoryHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateCategoryCommand, CategoryResult>
{
    public async Task<CategoryResult> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            UrlSlug = GenerateSlug(req.Name),
            Description = req.Description,
            ParentCategoryId = req.ParentCategoryId ?? Guid.Empty,
            SortOrder = req.SortOrder
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(category);
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(" ", "-").Replace(".", "").Replace(",", "");

    private static CategoryResult MapToResult(Category c) =>
        new(c.Id, c.Name, c.UrlSlug, c.Description, c.ParentCategoryId, c.SortOrder);
}
