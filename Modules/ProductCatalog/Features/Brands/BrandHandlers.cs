namespace ProductCatalog.Features.Brands;

internal class CreateBrandHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateBrandCommand, BrandResult>
{
    public async Task<BrandResult> Handle(CreateBrandCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            UrlSlug = req.UrlSlug,
            Description = req.Description,
            LogoUrl = req.LogoUrl
        };

        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(brand);
    }

    private static BrandResult MapToResult(Brand b) =>
        new(b.Id, b.Name, b.UrlSlug, b.Description, b.LogoUrl);
}

internal class UpdateBrandHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateBrandCommand, BrandResult>
{
    public async Task<BrandResult> Handle(UpdateBrandCommand command, CancellationToken cancellationToken)
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

        return new BrandResult(
            brand.Id, brand.Name, brand.UrlSlug, brand.Description, brand.LogoUrl
        );
    }
}

internal class DeleteBrandHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteBrandCommand, bool>
{
    public async Task<bool> Handle(DeleteBrandCommand command, CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FindAsync([command.Id], cancellationToken);
        if (brand is null)
            throw new KeyNotFoundException("Brand not found");

        // Check if brand has products
        var hasProducts = await dbContext.Products.AnyAsync(p => p.BrandId == command.Id, cancellationToken);
        if (hasProducts)
            throw new InvalidOperationException("Cannot delete brand with products");

        dbContext.Brands.Remove(brand);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
