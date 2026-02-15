using Promotion.Infrastructure.Entity;

namespace Promotion.Features.SaleCampaigns;

public record SaleCampaignResult(
    Guid Id,
    string Name,
    string? Slug,
    string? Description,
    string? BannerImageUrl,
    SaleCampaignType Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    bool IsFeatured,
    bool IsRunning,
    bool IsUpcoming,
    int ItemCount
);

public record SaleCampaignDetailResult(
    Guid Id,
    string Name,
    string? Slug,
    string? Description,
    string? BannerImageUrl,
    SaleCampaignType Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    bool IsFeatured,
    bool IsRunning,
    bool IsUpcoming,
    List<SaleCampaignItemResult> Items
);

public record SaleCampaignItemResult(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    decimal SalePrice,
    decimal? OriginalPrice,
    decimal? DiscountPercentage,
    int? MaxQuantity,
    int SoldQuantity,
    bool HasStock
);

public record SaleCampaignsResult(
    List<SaleCampaignResult> Items,
    int TotalCount,
    int Page,
    int PageSize
);
