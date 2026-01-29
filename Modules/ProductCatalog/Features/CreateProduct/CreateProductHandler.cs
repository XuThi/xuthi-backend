namespace ProductCatalog.Features.CreateProduct;

public record CreateProductRequest(
    string Name,
    string Description,
    Guid CategoryId,
    Guid BrandId,
    bool IsActive = true
);

public record CreateProductCommand(CreateProductRequest Request) : ICommand<CreateProductResult>;
public record CreateProductResult(Guid Id);

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.Request.Description).NotEmpty().WithMessage("Description is required");
        RuleFor(x => x.Request.CategoryId).NotEmpty().WithMessage("CategoryId is required");
        RuleFor(x => x.Request.BrandId).NotEmpty().WithMessage("BrandId is required");
    }
}

internal class CreateProductHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            UrlSlug = GenerateSlug(request.Name),
            Description = request.Description,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id);
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "");
    }
}