namespace Core.Caching;

/// <summary>
/// Centralized cache key constants and helpers.
/// Every cacheable query uses these prefixes so invalidation can target them by prefix.
/// </summary>
public static class CacheKeys
{
    public const string Products = "products";
    public const string Categories = "categories";
    public const string Brands = "brands";
    public const string SaleCampaigns = "sale-campaigns";
    public const string ActiveSaleItems = "active-sale-items";
    public const string Cart = "cart";

    /// <summary>
    /// Build a composite cache key from a prefix and segments.
    /// Example: Build("products", "active=true", "page=1") → "products:active=true:page=1"
    /// </summary>
    public static string Build(string prefix, params string[] segments)
        => segments.Length == 0 ? prefix : $"{prefix}:{string.Join(":", segments)}";
}
