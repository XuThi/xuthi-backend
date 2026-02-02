namespace ProductCatalog.Features.GetBrands;

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
