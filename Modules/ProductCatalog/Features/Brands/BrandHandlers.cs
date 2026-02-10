namespace ProductCatalog.Features.Brands;

public record CreateBrandResult(Guid Id, string Name, string UrlSlug, string? Description, string? LogoUrl);