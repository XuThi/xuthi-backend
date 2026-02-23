namespace Promotion.SaleCampaigns.Features.GetSaleCampaignBySlug;

public record GetSaleCampaignBySlugQuery(string Slug) : IQuery<SaleCampaignDetailResult>;

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
