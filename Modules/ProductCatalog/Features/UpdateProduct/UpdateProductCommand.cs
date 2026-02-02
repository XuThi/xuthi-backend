using Microsoft.AspNetCore.Http;

namespace ProductCatalog.Features.UpdateProduct;

public record UpdateProductCommand(
    Guid Id, 
    UpdateProductRequest Request, 
    List<IFormFile>? NewImages = null,
    List<Guid>? RemoveImageIds = null
) : ICommand<UpdateProductResult>;

public record UpdateProductRequest(
    string? Name,
    string? Description,
    Guid? CategoryId,
    Guid? BrandId,
    bool? IsActive,
    // Image IDs to remove
    List<Guid>? RemoveImageIds = null,
    // Reorder images: ImageId -> new SortOrder
    Dictionary<Guid, int>? ImageSortOrders = null
);

public record UpdateProductResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string Description,
    Guid CategoryId,
    Guid BrandId,
    bool IsActive,
    DateTime UpdatedAt,
    List<string> ImageUrls
);
