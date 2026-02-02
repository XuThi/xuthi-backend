namespace ProductCatalog.Features.SearchProducts;

/// <summary>
/// Search/filter request for products
/// </summary>
public record SearchProductsRequest(
    string? Query = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? IsActive = null,
    string? SortBy = null,
    bool SortDescending = false,
    int Page = 1,
    int PageSize = 10
);

public record SearchProductsQuery(SearchProductsRequest Request) : IQuery<SearchProductsResult>;

public record SearchProductsResult(
    List<ProductSearchItem> Products,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Product item matching frontend Product type
/// </summary>
public record ProductSearchItem(
    Guid Id,
    string Name,
    string Slug, // Frontend expects 'slug' not 'urlSlug'
    string? Summary,
    string? Description,
    List<string> Images, // Frontend expects array of image URLs
    Guid? CategoryId,
    string? CategoryName,
    Guid? BrandId,
    string? BrandName,
    bool IsActive,
    List<ProductVariantItem> Variants,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// Variant item matching frontend ProductVariant type
/// </summary>
public record ProductVariantItem(
    Guid Id,
    string Sku,
    string Name, // Variant description
    decimal Price,
    decimal? CompareAtPrice, // For discounts
    int StockQuantity, // Simplified - always show as available
    List<string> Images, // Variant-specific images
    Dictionary<string, string> Attributes // e.g. {"size": "37"}
);
