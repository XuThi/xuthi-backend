namespace ProductCatalog.Features.Categories.UpdateCategory;

public record UpdateCategoryCommand(Guid Id, UpdateCategoryRequest Request) : ICommand<CategoryResult>;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid ParentCategoryId,
    int SortOrder
);

internal class UpdateCategoryHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateCategoryCommand, CategoryResult>
{
    public async Task<CategoryResult> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FindAsync([command.Id], cancellationToken);
        if (category is null)
            throw new KeyNotFoundException("Category not found");

        var req = command.Request;

        if (req.Name != null)
        {
            category.Name = req.Name;
            category.UrlSlug = req.Name.ToLowerInvariant().Replace(" ", "-");
        }
        if (req.Description != null) category.Description = req.Description;
        if (req.ParentCategoryId.HasValue) category.ParentCategoryId = req.ParentCategoryId.Value;
        if (req.SortOrder.HasValue) category.SortOrder = req.SortOrder.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryResult(
            category.Id, category.Name, category.UrlSlug,
            category.Description, category.ParentCategoryId, category.SortOrder
        );
    }
}
