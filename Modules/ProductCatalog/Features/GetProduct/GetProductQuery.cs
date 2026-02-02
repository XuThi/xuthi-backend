namespace ProductCatalog.Features.GetProduct;

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
