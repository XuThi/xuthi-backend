namespace ProductCatalog.Features.DeleteProduct;

internal class DeleteProductHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteProductCommand, bool>
{
    public async Task<bool> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
            
        if (product is null)
            throw new KeyNotFoundException("Product not found");

        // Soft delete product and all its variants
        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;

        foreach (var variant in product.Variants)
        {
            variant.IsDeleted = true;
            variant.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
