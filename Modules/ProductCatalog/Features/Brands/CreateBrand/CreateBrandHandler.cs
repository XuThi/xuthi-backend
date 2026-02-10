namespace ProductCatalog.Features.Brands.CreateBrand;

public record CreateBrandCommand(string Name, string UrlSlug, string? Description, string? LogoUrl) : ICommand<CreateBrandResult>;
public record CreateBrandResult(Guid Id, string Name, string UrlSlug, string? Description, string? LogoUrl);

internal class CreateBrandHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateBrandCommand, CreateBrandResult>
{
    public async Task<CreateBrandResult> Handle(CreateBrandCommand command, CancellationToken cancellationToken)
    {
        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            UrlSlug = command.UrlSlug,
            Description = command.Description,
            LogoUrl = command.LogoUrl
        };

        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(brand);
    }

    private static CreateBrandResult MapToResult(Brand b) =>
        new(b.Id, b.Name, b.UrlSlug, b.Description, b.LogoUrl);
}