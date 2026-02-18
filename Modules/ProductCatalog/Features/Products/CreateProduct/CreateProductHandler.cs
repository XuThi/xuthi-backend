using Microsoft.AspNetCore.Http;
using ProductCatalog.Features.Media;

namespace ProductCatalog.Features.Products.CreateProduct;

public record CreateProductRequest(
    string Name,
    string Description,
    Guid CategoryId,
    Guid BrandId,
    bool IsActive = true,
    List<CreateVariantInput>? Variants = null,
    // Pre-uploaded image URLs (e.g. from Cloudinary)
    List<string>? Images = null
);

public record CreateVariantInput(
    string Sku,
    string? BarCode,
    decimal Price,
    decimal? CompareAtPrice,
    int? StockQuantity,
    string? Description,
    bool IsActive = true,
    List<OptionSelectionInput>? OptionSelections = null
);

public record OptionSelectionInput(string VariantOptionId, string Value);

public record CreateProductCommand(CreateProductRequest Request, List<IFormFile>? Images = null) : ICommand<CreateProductResult>;
public record CreateProductResult(Guid Id, List<Guid> VariantIds, List<string> ImageUrls);

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

internal class CreateProductHandler(
    ProductCatalogDbContext dbContext,
    ICloudinaryMediaService cloudinaryMediaService)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    
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

        // Create variants if provided
        var variantIds = new List<Guid>();
        if (request.Variants?.Count > 0)
        {
            foreach (var vr in request.Variants)
            {
                var variant = new Variant
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Sku = vr.Sku,
                    BarCode = vr.BarCode ?? vr.Sku, // Default barcode to SKU
                    Price = vr.Price,
                    CompareAtPrice = vr.CompareAtPrice,
                    StockQuantity = vr.StockQuantity ?? 0,
                    Description = vr.Description ?? "",
                    IsActive = vr.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Add option selections (e.g., Size=38)
                if (vr.OptionSelections?.Count > 0)
                {
                    variant.OptionSelections = vr.OptionSelections.Select(os => new VariantOptionSelection
                    {
                        VariantId = variant.Id,
                        VariantOptionId = os.VariantOptionId,
                        Value = os.Value
                    }).ToList();
                }

                product.Variants.Add(variant);
                variantIds.Add(variant.Id);
            }
        }

        // Upload images if provided via file uploads
        var imageUrls = new List<string>();
        if (command.Images?.Count > 0)
        {
            var sortOrder = 0;
            foreach (var file in command.Images)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                    continue;

                var uploadResult = await cloudinaryMediaService.UploadImageAsync(
                    file,
                    "products",
                    cancellationToken);

                var imageId = Guid.NewGuid();
                
                var image = new Image
                {
                    Id = imageId,
                    Url = uploadResult.Url,
                    CloudinaryPublicId = uploadResult.PublicId,
                    AltText = product.Name
                };

                dbContext.Images.Add(image);
                product.Images.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageId = image.Id,
                    SortOrder = sortOrder++
                });

                imageUrls.Add(uploadResult.Url);
            }
        }

        // Add pre-uploaded image URLs (e.g. from Cloudinary)
        if (request.Images?.Count > 0)
        {
            var sortOrder = product.Images.Count;
            foreach (var url in request.Images)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                
                var imageId = Guid.NewGuid();
                var image = new Image
                {
                    Id = imageId,
                    Url = url,
                    CloudinaryPublicId = null,
                    AltText = product.Name
                };

                dbContext.Images.Add(image);
                product.Images.Add(new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ImageId = image.Id,
                    SortOrder = sortOrder++
                });

                imageUrls.Add(url);
            }
        }

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id, variantIds, imageUrls);
    }

    private static string GenerateSlug(string name)
    {
        return name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "");
    }
}