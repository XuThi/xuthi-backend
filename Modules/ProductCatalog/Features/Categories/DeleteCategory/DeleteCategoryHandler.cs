namespace ProductCatalog.Features.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : ICommand<bool>;

internal class DeleteCategoryHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteCategoryCommand, bool>
{
    public async Task<bool> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FindAsync([command.Id], cancellationToken);
        if (category is null)
            throw new KeyNotFoundException("Category not found");

        var hasProducts = await dbContext.Products.AnyAsync(p => p.CategoryId == command.Id, cancellationToken);
        if (hasProducts)
            throw new InvalidOperationException("Cannot delete category with products");

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
