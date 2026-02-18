namespace ProductCatalog.Features.Products.GetProduct;

public record GetProductQuery(Guid? Id = null, string? Slug = null) : IQuery<ProductDetailResult>;

/// <summary>
/// Product detail matching frontend Product type exactly
/// </summary>
public record ProductDetailResult(
    Guid Id,
    string Name,
    string Slug, // Frontend expects 'slug' not 'urlSlug'
    string? Summary,
    string? Description,
    List<string> Images, // Frontend expects string[] of URLs
    Guid? CategoryId,
    string? CategoryName,
    Guid? BrandId,
    string? BrandName,
    bool IsActive,
    List<ProductVariantResult> Variants,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Variant matching frontend ProductVariant type
/// </summary>
public record ProductVariantResult(
    Guid Id,
    string Sku,
    string Name, // Variant description/name
    decimal Price,
    decimal? CompareAtPrice,
    int StockQuantity,
    List<string> Images, // Variant-specific images
    Dictionary<string, string> Attributes // e.g. {"size": "37"}
);

internal class GetProductHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetProductQuery, ProductDetailResult>
{
    public async Task<ProductDetailResult> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Build query with all related data
        var query = dbContext.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants)
                .ThenInclude(v => v.OptionSelections)
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        // Find by ID or slug
        Product? product = null;
        
        if (request.Id.HasValue)
        {
            product = await query.FirstOrDefaultAsync(p => p.Id == request.Id.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            product = await query.FirstOrDefaultAsync(p => p.UrlSlug == request.Slug, cancellationToken);
        }

        if (product is null)
        {
            throw new KeyNotFoundException($"Product not found");
        }

        // Map to result matching frontend types
        return MapToResult(product);
    }

    private static ProductDetailResult MapToResult(Product product)
    {
        var activeVariants = product.Variants.Where(v => !v.IsDeleted).ToList();
        
        var variants = activeVariants.Select(v => new ProductVariantResult(
            Id: v.Id,
            Sku: v.Sku,
            Name: v.Description, // Use description as variant name
            Price: v.Price,
            CompareAtPrice: v.CompareAtPrice,
            StockQuantity: v.StockQuantity,
            Images: [], // Variant-specific images - could load separately if needed
            Attributes: v.OptionSelections.ToDictionary(os => os.VariantOptionId, os => os.Value)
        )).ToList();
        
        return new ProductDetailResult(
            Id: product.Id,
            Name: product.Name,
            Slug: product.UrlSlug, // Frontend expects 'slug'
            Summary: null, // We don't have a separate summary field
            Description: product.Description,
            Images: product.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.Image?.Url)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url!)
                .ToList(),
            CategoryId: product.CategoryId,
            CategoryName: product.Category?.Name,
            BrandId: product.BrandId,
            BrandName: product.Brand?.Name,
            IsActive: product.IsActive,
            Variants: variants,
            CreatedAt: product.CreatedAt,
            UpdatedAt: product.UpdatedAt
        );
    }
}
