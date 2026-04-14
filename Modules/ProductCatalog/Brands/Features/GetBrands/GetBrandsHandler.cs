using Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace ProductCatalog.Brands.Features.GetBrands;

public record GetBrandsQuery() : IQuery<GetBrandsResult>;
public record GetBrandsResult(List<BrandItem> Brands);

internal class GetBrandsHandler(
    ProductCatalogDbContext dbContext,
    IMemoryCache cache,
    ICacheInvalidator cacheInvalidator)
    : IQueryHandler<GetBrandsQuery, GetBrandsResult>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<GetBrandsResult> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.Build(CacheKeys.Brands, "all");

        if (cache.TryGetValue(cacheKey, out GetBrandsResult? cached) && cached is not null)
            return cached;

        var brands = await dbContext.Brands
            .OrderBy(b => b.Name)
            .Select(b => new BrandItem(
                b.Id,
                b.Name,
                b.UrlSlug,
                b.Description,
                b.LogoUrl,
                dbContext.Products.Count(p => p.BrandId == b.Id && !p.IsDeleted && p.IsActive)
            ))
            .ToListAsync(cancellationToken);

        var result = new GetBrandsResult(brands);

        cache.Set(cacheKey, result, CacheDuration);
        cacheInvalidator.TrackKey(cacheKey);

        return result;
    }
}

public record BrandItem(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? LogoUrl,
    int ProductCount
);
