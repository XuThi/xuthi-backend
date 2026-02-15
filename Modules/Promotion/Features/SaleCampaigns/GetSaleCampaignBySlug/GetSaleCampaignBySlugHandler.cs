using Promotion.Infrastructure.Data;
using Promotion.Infrastructure.Entity;

namespace Promotion.Features.SaleCampaigns.GetSaleCampaignBySlug;

public record GetSaleCampaignBySlugQuery(string Slug) : IQuery<SaleCampaignDetailResult>;

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

internal class GetSaleCampaignBySlugHandler(PromotionDbContext dbContext)
    : IQueryHandler<GetSaleCampaignBySlugQuery, SaleCampaignDetailResult>
{
    public async Task<SaleCampaignDetailResult> Handle(GetSaleCampaignBySlugQuery query, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Slug == query.Slug, cancellationToken);

        if (campaign is null)
            throw new KeyNotFoundException("Sale campaign not found");

        return new SaleCampaignDetailResult(
            campaign.Id, campaign.Name, campaign.Slug, campaign.Description, campaign.BannerImageUrl,
            campaign.Type, campaign.StartDate, campaign.EndDate, campaign.IsActive, campaign.IsFeatured,
            campaign.IsRunning, campaign.IsUpcoming,
            campaign.Items.Select(i => new SaleCampaignItemResult(
                i.Id, i.ProductId, i.VariantId, i.SalePrice, i.OriginalPrice,
                i.DiscountPercentage, i.MaxQuantity, i.SoldQuantity, i.HasStock
            )).ToList()
        );
    }
}
