using Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Promotion.SaleCampaigns.Features.GetSaleCampaigns;

public record GetSaleCampaignsQuery(
    bool? IsActive = null,
    bool? IsFeatured = null,
    SaleCampaignType? Type = null,
    bool? OnlyRunning = null,
    bool? OnlyUpcoming = null,
    int Page = 1,
    int PageSize = 20
) : IQuery<SaleCampaignsResult>;

internal class GetSaleCampaignsHandler(
    PromotionDbContext dbContext,
    IMemoryCache cache,
    ICacheInvalidator cacheInvalidator)
    : IQueryHandler<GetSaleCampaignsQuery, SaleCampaignsResult>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<SaleCampaignsResult> Handle(GetSaleCampaignsQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.Build(CacheKeys.SaleCampaigns,
            $"active={query.IsActive}",
            $"featured={query.IsFeatured}",
            $"type={query.Type}",
            $"running={query.OnlyRunning}",
            $"upcoming={query.OnlyUpcoming}",
            $"p={query.Page}",
            $"ps={query.PageSize}");

        if (cache.TryGetValue(cacheKey, out SaleCampaignsResult? cached) && cached is not null)
            return cached;

        var q = dbContext.SaleCampaigns.Include(c => c.Items).AsQueryable();

        if (query.IsActive.HasValue)
            q = q.Where(c => c.IsActive == query.IsActive.Value);

        if (query.IsFeatured.HasValue)
            q = q.Where(c => c.IsFeatured == query.IsFeatured.Value);

        if (query.Type.HasValue)
            q = q.Where(c => c.Type == query.Type.Value);

        var now = DateTime.UtcNow;
        if (query.OnlyRunning == true)
            q = q.Where(c => c.IsActive && c.StartDate <= now && c.EndDate >= now);

        if (query.OnlyUpcoming == true)
            q = q.Where(c => c.IsActive && c.StartDate > now);

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(c => c.IsFeatured)
            .ThenByDescending(c => c.StartDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var result = new SaleCampaignsResult(
            items.Select(c => new SaleCampaignResult(
                c.Id, c.Name, c.Slug, c.Description, c.BannerImageUrl,
                c.Type, c.StartDate, c.EndDate, c.IsActive, c.IsFeatured,
                c.IsRunning, c.IsUpcoming, c.Items.Count
            )).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );

        cache.Set(cacheKey, result, CacheDuration);
        cacheInvalidator.TrackKey(cacheKey);

        return result;
    }
}
