namespace Promotion.SaleCampaigns.Features.GetSaleCampaign;

public record GetSaleCampaignQuery(Guid Id) : IQuery<SaleCampaignDetailResult>;

internal class GetSaleCampaignHandler(PromotionDbContext dbContext)
    : IQueryHandler<GetSaleCampaignQuery, SaleCampaignDetailResult>
{
    public async Task<SaleCampaignDetailResult> Handle(GetSaleCampaignQuery query, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.SaleCampaigns
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);

        if (campaign is null)
            throw new KeyNotFoundException("Sale campaign not found");

        return MapToDetailResult(campaign);
    }

    private static SaleCampaignDetailResult MapToDetailResult(SaleCampaign c) => new(
        c.Id, c.Name, c.Slug, c.Description, c.BannerImageUrl,
        c.Type, c.StartDate, c.EndDate, c.IsActive, c.IsFeatured,
        c.IsRunning, c.IsUpcoming,
        c.Items.Select(i => new SaleCampaignItemResult(
            i.Id, i.ProductId, i.VariantId, i.SalePrice, i.OriginalPrice,
            i.DiscountPercentage, i.MaxQuantity, i.SoldQuantity, i.HasStock
        )).ToList()
    );
}
