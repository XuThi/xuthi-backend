namespace ProductCatalog.Features.Brands.GetBrands;

public record GetBrandsQuery() : IQuery<GetBrandsResult>;
public record GetBrandsResult(List<BrandItem> Brands);

internal class GetBrandsHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetBrandsQuery, GetBrandsResult>
{
    public async Task<GetBrandsResult> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
    {
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

        return new GetBrandsResult(brands);
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