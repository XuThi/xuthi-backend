using Microsoft.AspNetCore.Http;
using ProductCatalog.Products.Events;
using ProductCatalog.Products.Features.Media;

namespace ProductCatalog.Products.Features.CreateProduct;

public record CreateProductRequest(
    string Name,
    string Description,
    Guid CategoryId,
    Guid BrandId,
    bool IsActive = true,
    bool NotifySubscribers = false,
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
            var validFiles = command.Images
                .Where(file => AllowedExtensions.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
                .ToList();

            if (validFiles.Count == 0)
            {
                throw new InvalidOperationException("Định dạng ảnh không được hỗ trợ. Chỉ chấp nhận: jpg, jpeg, png, webp, gif.");
            }

            foreach (var file in validFiles)
            {
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

        // Raise domain event for subscribers notification (only if opted in)
        if (request.NotifySubscribers)
        {
            var firstImageUrl = imageUrls.Count > 0 ? imageUrls[0] : null;
            var basePrice = product.Variants.Count > 0 ? product.Variants.Min(v => v.Price) : (decimal?)null;
            product.AddDomainEvent(new ProductCreatedEvent(product.Id, product.Name, firstImageUrl, product.UrlSlug, basePrice));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id, variantIds, imageUrls);
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.Replace("đ", "d").Replace("Đ", "d")
            .Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in slug)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        slug = sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
        return slug;
    }
}
