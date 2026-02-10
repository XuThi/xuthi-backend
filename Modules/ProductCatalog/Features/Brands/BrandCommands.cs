namespace ProductCatalog.Features.Brands;

// ============== CREATE ==============
public record CreateBrandCommand(CreateBrandRequest Request) : ICommand<BrandResult>;

public record CreateBrandRequest(
    string Name,
    string UrlSlug,
    string? Description,
    string? LogoUrl
);

// ============== UPDATE ==============
public record UpdateBrandCommand(Guid Id, UpdateBrandRequest Request) : ICommand<BrandResult>;

public record UpdateBrandRequest(
    string? Name,
    string? UrlSlug,
    string? Description,
    string? LogoUrl
);

// ============== DELETE ==============
public record DeleteBrandCommand(Guid Id) : ICommand<bool>;
// ============== RESULT ==============
public record BrandResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? LogoUrl
);
