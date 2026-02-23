namespace ProductCatalog.Brands.Features.GetBrandById;

public record GetBrandByIdQuery(Guid Id) : IQuery<BrandDetailResult>;

public record BrandDetailResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? LogoUrl,
    int ProductCount
);

internal class GetBrandByIdHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetBrandByIdQuery, BrandDetailResult>
{
    public async Task<BrandDetailResult> Handle(GetBrandByIdQuery query, CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FindAsync([query.Id], cancellationToken);
        if (brand is null)
            throw new KeyNotFoundException("Brand not found");

        var productCount = await dbContext.Products
            .CountAsync(p => p.BrandId == brand.Id && !p.IsDeleted && p.IsActive, cancellationToken);

        return new BrandDetailResult(
            brand.Id,
            brand.Name,
            brand.UrlSlug,
            brand.Description,
            brand.LogoUrl,
            productCount
        );
    }
}
