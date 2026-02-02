namespace ProductCatalog.Features.Categories;

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

internal class DeleteCategoryHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteCategoryCommand, bool>
{
    public async Task<bool> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FindAsync([command.Id], cancellationToken);
        if (category is null)
            throw new KeyNotFoundException("Category not found");

        // Check if category has products
        var hasProducts = await dbContext.Products.AnyAsync(p => p.CategoryId == command.Id, cancellationToken);
        if (hasProducts)
            throw new InvalidOperationException("Cannot delete category with products");

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
