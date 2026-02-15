namespace Promotion.Features.SaleCampaigns;

public record CreateSaleCampaignItemRequest(
    Guid ProductId,
    Guid? VariantId,
    decimal SalePrice,
    decimal? OriginalPrice,
    decimal? DiscountPercentage,
    int? MaxQuantity
);

public record UpdateSaleCampaignItemRequest(
    decimal? SalePrice = null,
    decimal? OriginalPrice = null,
    decimal? DiscountPercentage = null,
    int? MaxQuantity = null
);
