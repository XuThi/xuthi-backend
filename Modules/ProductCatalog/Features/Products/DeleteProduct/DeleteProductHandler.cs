namespace ProductCatalog.Features.Products.DeleteProduct;
using ProductCatalog.Features.Media;

// TODO: Figure out why the fuck do we do Raw SQL here

public record DeleteProductCommand(Guid Id) : ICommand<bool>;

internal class DeleteProductHandler(
    ProductCatalogDbContext dbContext,
    ICloudinaryMediaService cloudinaryMediaService)
    : ICommandHandler<DeleteProductCommand, bool>
{
    public async Task<bool> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var hasOrderReferences = await dbContext.Database
            .SqlQuery<int>($"SELECT 1 FROM \"OrderItems\" WHERE \"ProductId\" = {command.Id} LIMIT 1")
            .AnyAsync(cancellationToken);

        if (hasOrderReferences)
            throw new InvalidOperationException("Không thể xoá sản phẩm vì đã phát sinh trong đơn hàng.");

        var product = await dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
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

        foreach (var image in product.Images)
        {
            await cloudinaryMediaService.DeleteImageAsync(image.Image?.CloudinaryPublicId, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
