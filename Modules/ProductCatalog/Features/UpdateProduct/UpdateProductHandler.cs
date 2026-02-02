namespace ProductCatalog.Features.UpdateProduct;

internal class UpdateProductHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateProductCommand, UpdateProductResult>
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    
    public async Task<UpdateProductResult> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .FirstOrDefaultAsync(p => p.Id == command.Id && !p.IsDeleted, cancellationToken);
            
        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var req = command.Request;

        if (req.Name != null)
        {
            product.Name = req.Name;
            product.UrlSlug = GenerateSlug(req.Name);
        }
        if (req.Description != null) product.Description = req.Description;
        if (req.CategoryId.HasValue) product.CategoryId = req.CategoryId.Value;
        if (req.BrandId.HasValue) product.BrandId = req.BrandId.Value;
        if (req.IsActive.HasValue) product.IsActive = req.IsActive.Value;

        // Remove specified images
        var removeIds = command.RemoveImageIds ?? req.RemoveImageIds;
        if (removeIds?.Count > 0)
        {
            var imagesToRemove = product.Images.Where(pi => removeIds.Contains(pi.ImageId)).ToList();
            foreach (var pi in imagesToRemove)
            {
                product.Images.Remove(pi);
                // TODO: Delete from Cloudinary if CloudinaryPublicId is set
                dbContext.ProductImages.Remove(pi);
                dbContext.Images.Remove(pi.Image);
            }
        }

        // Update image sort orders
        if (req.ImageSortOrders?.Count > 0)
        {
            foreach (var (imageId, sortOrder) in req.ImageSortOrders)
            {
                var pi = product.Images.FirstOrDefault(x => x.ImageId == imageId);
                if (pi != null)
                    pi.SortOrder = sortOrder;
            }
        }

        // Upload new images
        if (command.NewImages?.Count > 0)
        {
            var maxSort = product.Images.Any() ? product.Images.Max(x => x.SortOrder) : -1;
            
            foreach (var file in command.NewImages)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                    continue;

                // TODO: Replace with Cloudinary upload
                var imageId = Guid.NewGuid();
                var placeholderUrl = $"/uploads/products/{imageId}{extension}";
                
                var image = new Image
                {
                    Id = imageId,
                    Url = placeholderUrl,
                    CloudinaryPublicId = null,
                    AltText = product.Name
                };

                dbContext.Images.Add(image);
                product.Images.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageId = image.Id,
                    SortOrder = ++maxSort
                });
            }
        }

        product.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var imageUrls = product.Images
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Image.Url)
            .ToList();

        return new UpdateProductResult(
            product.Id,
            product.Name,
            product.UrlSlug,
            product.Description,
            product.CategoryId,
            product.BrandId,
            product.IsActive,
            product.UpdatedAt,
            imageUrls
        );
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(" ", "-").Replace(".", "").Replace(",", "");
}
