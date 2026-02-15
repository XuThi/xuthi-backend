namespace ProductCatalog.Features.Brands.UpdateBrand;

public record UpdateBrandCommand(Guid Id, UpdateBrandRequest Request) : ICommand<UpdateBrandResult>;

public record UpdateBrandResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? LogoUrl
);

internal class UpdateBrandHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateBrandCommand, UpdateBrandResult>
{
    public async Task<UpdateBrandResult> Handle(UpdateBrandCommand command, CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FindAsync([command.Id], cancellationToken);
        if (brand is null)
            throw new KeyNotFoundException("Brand not found");

        var req = command.Request;

        if (req.Name != null) brand.Name = req.Name;
        if (req.UrlSlug != null) brand.UrlSlug = req.UrlSlug;
        if (req.Description != null) brand.Description = req.Description;
        if (req.LogoUrl != null) brand.LogoUrl = req.LogoUrl;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateBrandResult(
            brand.Id,
            brand.Name,
            brand.UrlSlug,
            brand.Description,
            brand.LogoUrl
        );
    }
}
