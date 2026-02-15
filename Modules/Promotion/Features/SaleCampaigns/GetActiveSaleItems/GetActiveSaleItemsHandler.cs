using Microsoft.EntityFrameworkCore;
using Promotion.Infrastructure.Data;

namespace Promotion.Features.SaleCampaigns.GetActiveSaleItems;

public record GetActiveSaleItemsQuery(
    List<Guid> ProductIds,
    List<Guid> VariantIds
) : IQuery<GetActiveSaleItemsResult>;

public record GetActiveSaleItemsResult(List<ActiveSaleItemResult> Items);

public record ActiveSaleItemResult(
    Guid Id,
    Guid CampaignId,
    string CampaignName,
    Guid ProductId,
    Guid? VariantId,
    decimal SalePrice,
    decimal? OriginalPrice,
    decimal? DiscountPercentage
);

internal class GetActiveSaleItemsHandler(PromotionDbContext dbContext)
    : IQueryHandler<GetActiveSaleItemsQuery, GetActiveSaleItemsResult>
{
    public async Task<GetActiveSaleItemsResult> Handle(GetActiveSaleItemsQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var itemsQuery = dbContext.SaleCampaignItems
            .Include(i => i.SaleCampaign)
            .Where(i => i.SaleCampaign.IsActive && i.SaleCampaign.StartDate <= now && i.SaleCampaign.EndDate >= now);

        if (query.ProductIds.Count > 0)
        {
            itemsQuery = itemsQuery.Where(i => query.ProductIds.Contains(i.ProductId));
        }

        if (query.VariantIds.Count > 0)
        {
            itemsQuery = itemsQuery.Where(i =>
                (i.VariantId.HasValue && query.VariantIds.Contains(i.VariantId.Value))
                || (i.VariantId == null && (query.ProductIds.Count == 0 || query.ProductIds.Contains(i.ProductId))));
        }

        var items = await itemsQuery
            .OrderByDescending(i => i.VariantId.HasValue)
            .ThenBy(i => i.SalePrice)
            .ToListAsync(ct);

        var result = items.Select(i => new ActiveSaleItemResult(
            i.Id,
            i.SaleCampaignId,
            i.SaleCampaign.Name,
            i.ProductId,
            i.VariantId,
            i.SalePrice,
            i.OriginalPrice,
            i.DiscountPercentage
        )).ToList();

        return new GetActiveSaleItemsResult(result);
    }
}
