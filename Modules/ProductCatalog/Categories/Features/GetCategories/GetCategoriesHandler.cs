using Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace ProductCatalog.Categories.Features.GetCategories;

public record GetCategoriesQuery(Guid? ParentId = null) : IQuery<GetCategoriesResult>;

public record GetCategoriesResult(List<CategoryItem> Categories);

public record CategoryItem(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? ImageUrl,
    Guid ParentCategoryId,
    int SortOrder,
    int ProductCount
);

internal class GetCategoriesHandler(
    ProductCatalogDbContext dbContext,
    IMemoryCache cache,
    ICacheInvalidator cacheInvalidator)
    : IQueryHandler<GetCategoriesQuery, GetCategoriesResult>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<GetCategoriesResult> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.Build(CacheKeys.Categories, $"parent={request.ParentId}");

        if (cache.TryGetValue(cacheKey, out GetCategoriesResult? cached) && cached is not null)
            return cached;

        var query = dbContext.Categories.AsQueryable();

        // Filter by parent category if specified
        if (request.ParentId.HasValue)
        {
            query = query.Where(c => c.ParentCategoryId == request.ParentId.Value);
        }

        var categories = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryItem(
                c.Id,
                c.Name,
                c.UrlSlug,
                c.Description,
                c.ImageUrl,
                c.ParentCategoryId,
                c.SortOrder,
                dbContext.Products.Count(p => p.CategoryId == c.Id && !p.IsDeleted && p.IsActive)
            ))
            .ToListAsync(cancellationToken);

        var result = new GetCategoriesResult(categories);

        cache.Set(cacheKey, result, CacheDuration);
        cacheInvalidator.TrackKey(cacheKey);

        return result;
    }
}
