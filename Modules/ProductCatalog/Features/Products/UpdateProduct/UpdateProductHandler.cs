namespace ProductCatalog.Features.Products.UpdateProduct;

public record UpdateProductCommand(
    Guid Id, 
    UpdateProductRequest Request, 
    List<IFormFile>? NewImages = null,
    List<Guid>? RemoveImageIds = null
) : ICommand<UpdateProductResult>;

public record UpdateProductRequest(
    string? Name,
    string? Description,
    Guid? CategoryId,
    Guid? BrandId,
    bool? IsActive,
    // Image IDs to remove
    List<Guid>? RemoveImageIds = null,
    // Reorder images: ImageId -> new SortOrder
    Dictionary<Guid, int>? ImageSortOrders = null,
    // Pre-uploaded image URLs (e.g. from Cloudinary)
    List<string>? Images = null,
    // Variants — full replace strategy
    List<UpdateVariantInput>? Variants = null
);

public record UpdateVariantInput(
    Guid? Id,  // null = new variant, non-null = update existing by Id
    string Sku,
    string? BarCode,
    decimal Price,
    decimal? CompareAtPrice,
    int? StockQuantity,
    string? Description,
    bool IsActive = true,
    List<OptionSelectionInput>? OptionSelections = null
);

public record OptionSelectionInput(string VariantOptionId, string Value);

public record UpdateProductResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string Description,
    Guid CategoryId,
    Guid BrandId,
    bool IsActive,
    DateTime UpdatedAt,
    List<string> ImageUrls
);

internal class UpdateProductHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateProductCommand, UpdateProductResult>
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    
    public async Task<UpdateProductResult> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .Include(p => p.Variants)
                .ThenInclude(v => v.OptionSelections)
            .FirstOrDefaultAsync(p => p.Id == command.Id && !p.IsDeleted, cancellationToken);
            
        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var req = command.Request;

        // ---- Basic fields ----
        if (req.Name != null)
        {
            product.Name = req.Name;
            product.UrlSlug = GenerateSlug(req.Name);
        }
        if (req.Description != null) product.Description = req.Description;
        if (req.CategoryId.HasValue) product.CategoryId = req.CategoryId.Value;
        if (req.BrandId.HasValue) product.BrandId = req.BrandId.Value;
        if (req.IsActive.HasValue) product.IsActive = req.IsActive.Value;

        // ---- Remove specified images ----
        var removeIds = command.RemoveImageIds ?? req.RemoveImageIds;
        if (removeIds?.Count > 0)
        {
            var imagesToRemove = product.Images.Where(pi => removeIds.Contains(pi.ImageId)).ToList();
            foreach (var pi in imagesToRemove)
            {
                product.Images.Remove(pi);
                dbContext.ProductImages.Remove(pi);
                dbContext.Images.Remove(pi.Image);
            }
        }

        // ---- Update image sort orders ----
        if (req.ImageSortOrders?.Count > 0)
        {
            foreach (var (imageId, sortOrder) in req.ImageSortOrders)
            {
                var pi = product.Images.FirstOrDefault(x => x.ImageId == imageId);
                if (pi != null)
                    pi.SortOrder = sortOrder;
            }
        }

        // ---- Upload new images via file upload ----
        if (command.NewImages?.Count > 0)
        {
            var maxSort = product.Images.Any() ? product.Images.Max(x => x.SortOrder) : -1;
            
            foreach (var file in command.NewImages)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                    continue;

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

        // ---- Add pre-uploaded image URLs (e.g. from Cloudinary) ----
        if (req.Images?.Count > 0)
        {
            // Collect existing URLs to avoid duplicates
            var existingUrls = product.Images.Select(pi => pi.Image.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var maxSort = product.Images.Any() ? product.Images.Max(x => x.SortOrder) : -1;

            foreach (var url in req.Images)
            {
                if (string.IsNullOrWhiteSpace(url) || existingUrls.Contains(url))
                    continue;

                var imageId = Guid.NewGuid();
                var image = new Image
                {
                    Id = imageId,
                    Url = url,
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

        // ---- Update variants (full replace strategy) ----
        if (req.Variants != null)
        {
            var incomingIds = req.Variants
                .Where(v => v.Id.HasValue)
                .Select(v => v.Id!.Value)
                .ToHashSet();

            // Soft-delete variants not in the incoming set
            foreach (var existing in product.Variants)
            {
                if (!incomingIds.Contains(existing.Id))
                {
                    existing.IsDeleted = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }

            foreach (var vr in req.Variants)
            {
                if (vr.Id.HasValue)
                {
                    // Update existing variant
                    var existing = product.Variants.FirstOrDefault(v => v.Id == vr.Id.Value);
                    if (existing != null)
                    {
                        existing.Sku = vr.Sku;
                        existing.BarCode = vr.BarCode ?? vr.Sku;
                        existing.Price = vr.Price;
                        existing.Description = vr.Description ?? "";
                        existing.IsActive = vr.IsActive;
                        existing.IsDeleted = false;
                        existing.UpdatedAt = DateTime.UtcNow;

                        // Replace option selections
                        if (existing.OptionSelections.Count > 0)
                        {
                            dbContext.VariantOptionSelections.RemoveRange(existing.OptionSelections);
                            existing.OptionSelections.Clear();
                        }

                        if (vr.OptionSelections?.Count > 0)
                        {
                            existing.OptionSelections = vr.OptionSelections.Select(os =>
                                new VariantOptionSelection
                                {
                                    VariantId = existing.Id,
                                    VariantOptionId = os.VariantOptionId,
                                    Value = os.Value
                                }).ToList();
                        }
                    }
                    else
                    {
                        // If the ID doesn't exist, create as new to avoid orphan selections
                        var variant = new Variant
                        {
                            Id = vr.Id.Value,
                            ProductId = product.Id,
                            Sku = vr.Sku,
                            BarCode = vr.BarCode ?? vr.Sku,
                            Price = vr.Price,
                            Description = vr.Description ?? "",
                            IsActive = vr.IsActive,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        if (vr.OptionSelections?.Count > 0)
                        {
                            variant.OptionSelections = vr.OptionSelections.Select(os =>
                                new VariantOptionSelection
                                {
                                    VariantId = variant.Id,
                                    VariantOptionId = os.VariantOptionId,
                                    Value = os.Value
                                }).ToList();
                        }

                        product.Variants.Add(variant);
                        dbContext.Variants.Add(variant);
                    }
                }
                else
                {
                    // Create new variant
                    var variant = new Variant
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        Sku = vr.Sku,
                        BarCode = vr.BarCode ?? vr.Sku,
                        Price = vr.Price,
                        Description = vr.Description ?? "",
                        IsActive = vr.IsActive,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (vr.OptionSelections?.Count > 0)
                    {
                        variant.OptionSelections = vr.OptionSelections.Select(os =>
                            new VariantOptionSelection
                            {
                                VariantId = variant.Id,
                                VariantOptionId = os.VariantOptionId,
                                Value = os.Value
                            }).ToList();
                    }

                    product.Variants.Add(variant);
                    dbContext.Variants.Add(variant);
                }
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
