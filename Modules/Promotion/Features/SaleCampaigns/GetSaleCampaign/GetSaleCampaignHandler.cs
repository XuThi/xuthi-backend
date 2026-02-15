namespace Promotion.Features.SaleCampaigns.GetSaleCampaign;

public record GetSaleCampaignQuery(Guid Id) : IQuery<SaleCampaignDetailResult>;

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
