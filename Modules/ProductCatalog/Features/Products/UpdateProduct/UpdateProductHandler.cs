namespace ProductCatalog.Features.Products.UpdateProduct;
using ProductCatalog.Features.Media;

// TODO: Yes try catch blocks again

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

internal class UpdateProductHandler(
    ProductCatalogDbContext dbContext,
    ICloudinaryMediaService cloudinaryMediaService)
    : ICommandHandler<UpdateProductCommand, UpdateProductResult>
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    
    public async Task<UpdateProductResult> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == command.Id && !p.IsDeleted, cancellationToken);
            
        if (product is null)
            throw new KeyNotFoundException("Product not found");

        var req = command.Request;
        var pendingOptionSelectionUpdates = new List<(Guid VariantId, List<OptionSelectionInput> Selections)>();
        var now = DateTime.UtcNow;

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

        // ---- Images ----
        var existingProductImages = await dbContext.ProductImages
            .Where(pi => pi.ProductId == product.Id)
            .Include(pi => pi.Image)
            .ToListAsync(cancellationToken);

        var removeImageIdSet = new HashSet<Guid>(command.RemoveImageIds ?? req.RemoveImageIds ?? []);

        if (req.Images != null)
        {
            var keepUrls = req.Images
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var existing in existingProductImages)
            {
                var url = existing.Image?.Url;
                if (string.IsNullOrWhiteSpace(url) || !keepUrls.Contains(url))
                    removeImageIdSet.Add(existing.ImageId);
            }
        }

        if (removeImageIdSet.Count > 0)
        {
            var rowsToRemove = existingProductImages
                .Where(pi => removeImageIdSet.Contains(pi.ImageId))
                .ToList();

            foreach (var row in rowsToRemove)
            {
                await cloudinaryMediaService.DeleteImageAsync(row.Image?.CloudinaryPublicId, cancellationToken);
            }

            await dbContext.ProductImages
                .Where(pi => pi.ProductId == product.Id && removeImageIdSet.Contains(pi.ImageId))
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.Images
                .Where(i => removeImageIdSet.Contains(i.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var activeProductImages = existingProductImages
            .Where(pi => !removeImageIdSet.Contains(pi.ImageId))
            .ToList();

        if (req.ImageSortOrders?.Count > 0)
        {
            foreach (var (imageId, sortOrder) in req.ImageSortOrders)
            {
                await dbContext.ProductImages
                    .Where(pi => pi.ProductId == product.Id && pi.ImageId == imageId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(pi => pi.SortOrder, sortOrder), cancellationToken);
            }
        }

        var existingUrls = activeProductImages
                .Select(pi => pi.Image?.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var maxSort = activeProductImages.Any() ? activeProductImages.Max(x => x.SortOrder) : -1;

        // ---- Add pre-uploaded image URLs (e.g. from Cloudinary) ----
        if (req.Images != null)
        {
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
                dbContext.ProductImages.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageId = image.Id,
                    SortOrder = ++maxSort
                });

                existingUrls.Add(url);
            }
        }

        // ---- Upload new images via file upload ----
        if (command.NewImages?.Count > 0)
        {
            foreach (var file in command.NewImages)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                    continue;

                var uploadResult = await cloudinaryMediaService.UploadImageAsync(
                    file,
                    "products",
                    cancellationToken);

                var imageId = Guid.NewGuid();

                var image = new Image
                {
                    Id = imageId,
                    Url = uploadResult.Url,
                    CloudinaryPublicId = uploadResult.PublicId,
                    AltText = product.Name
                };

                dbContext.Images.Add(image);
                dbContext.ProductImages.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageId = image.Id,
                    SortOrder = ++maxSort
                });

                existingUrls.Add(uploadResult.Url);
            }
        }

        // ---- Update variants (full replace strategy with set-based writes) ----
        if (req.Variants != null)
        {
            var providedIds = req.Variants
                .Where(v => v.Id.HasValue)
                .Select(v => v.Id!.Value)
                .Distinct()
                .ToList();

            var variantOwnership = providedIds.Count == 0
                ? new Dictionary<Guid, Guid>()
                : await dbContext.Variants
                    .Where(v => providedIds.Contains(v.Id))
                    .ToDictionaryAsync(v => v.Id, v => v.ProductId, cancellationToken);

            var incomingOwnedIds = req.Variants
                .Where(v => v.Id.HasValue)
                .Select(v => v.Id!.Value)
                .Where(id => variantOwnership.TryGetValue(id, out var ownerId) && ownerId == product.Id)
                .ToHashSet();

            await dbContext.Variants
                .Where(v => v.ProductId == product.Id && !incomingOwnedIds.Contains(v.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.IsDeleted, true)
                    .SetProperty(v => v.UpdatedAt, now), cancellationToken);

            foreach (var vr in req.Variants)
            {
                Guid variantId;
                var updateExisting = false;

                if (vr.Id.HasValue &&
                    variantOwnership.TryGetValue(vr.Id.Value, out var ownerProductId) &&
                    ownerProductId == product.Id)
                {
                    variantId = vr.Id.Value;
                    updateExisting = true;
                }
                else
                {
                    if (vr.Id.HasValue && !variantOwnership.ContainsKey(vr.Id.Value))
                        variantId = vr.Id.Value;
                    else
                        variantId = Guid.NewGuid();
                }

                if (updateExisting)
                {
                    await dbContext.Variants
                        .Where(v => v.Id == variantId)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(v => v.Sku, vr.Sku)
                            .SetProperty(v => v.BarCode, vr.BarCode ?? vr.Sku)
                            .SetProperty(v => v.Price, vr.Price)
                            .SetProperty(v => v.CompareAtPrice, vr.CompareAtPrice)
                            .SetProperty(v => v.StockQuantity, vr.StockQuantity ?? 0)
                            .SetProperty(v => v.Description, vr.Description ?? "")
                            .SetProperty(v => v.IsActive, vr.IsActive)
                            .SetProperty(v => v.IsDeleted, false)
                            .SetProperty(v => v.UpdatedAt, now), cancellationToken);
                }
                else
                {
                    var variant = new Variant
                    {
                        Id = variantId,
                        ProductId = product.Id,
                        Sku = vr.Sku,
                        BarCode = vr.BarCode ?? vr.Sku,
                        Price = vr.Price,
                        CompareAtPrice = vr.CompareAtPrice,
                        StockQuantity = vr.StockQuantity ?? 0,
                        Description = vr.Description ?? "",
                        IsActive = vr.IsActive,
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    dbContext.Variants.Add(variant);
                }

                pendingOptionSelectionUpdates.Add((variantId, vr.OptionSelections ?? []));
            }
        }

        product.UpdatedAt = now;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var resolved = await ResolveConcurrencyConflictsAsync(ex, cancellationToken);
            if (!resolved)
                throw;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (pendingOptionSelectionUpdates.Count > 0)
        {
            var variantIds = pendingOptionSelectionUpdates
                .Select(x => x.VariantId)
                .Distinct()
                .ToList();

            var existingVariantIds = await dbContext.Variants
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => v.Id)
                .ToHashSetAsync(cancellationToken);

            var existingVariantIdsList = existingVariantIds.ToList();
            if (existingVariantIdsList.Count > 0)
            {
                await dbContext.VariantOptionSelections
                    .Where(x => existingVariantIdsList.Contains(x.VariantId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            var newSelectionRows = pendingOptionSelectionUpdates
                .Where(x => existingVariantIds.Contains(x.VariantId))
                .SelectMany(x => x.Selections.Select(selection => new VariantOptionSelection
                {
                    VariantId = x.VariantId,
                    VariantOptionId = selection.VariantOptionId,
                    Value = selection.Value,
                }))
                .ToList();

            if (newSelectionRows.Count > 0)
            {
                dbContext.VariantOptionSelections.AddRange(newSelectionRows);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var imageUrls = product.Images
            .OrderBy(x => x.SortOrder)
            .Select(x => x.Image?.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
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

    private static async Task<bool> ResolveConcurrencyConflictsAsync(
        DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken)
    {
        var resolvedAny = false;

        foreach (var entry in exception.Entries)
        {
            if (entry.State == EntityState.Deleted)
            {
                var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                if (databaseValues is null)
                {
                    entry.State = EntityState.Detached;
                    resolvedAny = true;
                    continue;
                }

                entry.OriginalValues.SetValues(databaseValues);
                resolvedAny = true;
                continue;
            }

            if (entry.State == EntityState.Modified)
            {
                var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                if (databaseValues is null)
                    return false;

                entry.OriginalValues.SetValues(databaseValues);
                resolvedAny = true;
                continue;
            }

            return false;
        }

        return resolvedAny;
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant().Replace(" ", "-").Replace(".", "").Replace(",", "");
}
