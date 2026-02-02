namespace ProductCatalog.Features.GetBrands;

public record GetBrandsQuery() : IQuery<GetBrandsResult>;

public record GetBrandsResult(List<BrandItem> Brands);

public record BrandItem(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? LogoUrl,
    int ProductCount
);
