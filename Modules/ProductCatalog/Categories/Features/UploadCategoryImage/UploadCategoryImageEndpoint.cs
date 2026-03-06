using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Products.Features.Media;

namespace ProductCatalog.Categories.Features.UploadCategoryImage;

public record UploadCategoryImageResponse(string ImageUrl);

public class UploadCategoryImageEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories/{id:guid}/image", async (
            Guid id,
            [FromForm(Name = "image")] IFormFile image,
            ProductCatalogDbContext dbContext,
            ICloudinaryMediaService cloudinary,
            CancellationToken ct) =>
        {
            var category = await dbContext.Categories.FindAsync([id], ct);
            if (category is null)
                return Results.NotFound("Category not found");

            // Delete old image if exists
            if (!string.IsNullOrEmpty(category.ImagePublicId))
            {
                await cloudinary.DeleteImageAsync(category.ImagePublicId, ct);
            }

            var (url, publicId) = await cloudinary.UploadImageAsync(image, "categories", ct);

            category.ImageUrl = url;
            category.ImagePublicId = publicId;
            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(new UploadCategoryImageResponse(url));
        })
        .WithName("UploadCategoryImage")
        .Produces<UploadCategoryImageResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Upload Category Image")
        .WithDescription("Upload an image for a category (replaces existing image)")
        .WithTags("Categories")
        .DisableAntiforgery()
        .RequireAuthorization("Staff");
    }
}
