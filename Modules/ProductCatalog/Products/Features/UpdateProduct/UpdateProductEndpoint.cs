using System.Text.Json;
using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace ProductCatalog.Products.Features.UpdateProduct;

public record UpdateProductResponse(
    Guid Id,
    string Name,
    string UrlSlug,
    string Description,
    Guid CategoryId,
    Guid BrandId,
    bool IsActive,
    DateTime UpdatedAt,
    List<string> ImageUrls);

public class UpdateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // JSON endpoint for simple updates (no file uploads)
        app.MapPut("/api/products/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(id, request);
            var result = await sender.Send(command);
            var response = result.Adapt<UpdateProductResponse>();
            return Results.Ok(response);
        })
        .WithName("UpdateProduct")
        .Produces<UpdateProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Product")
        .WithDescription("Update product details (JSON, no file uploads)")
        .WithTags("Products");

        // Multipart form endpoint for updates with images
        app.MapPut("/api/products/{id:guid}/with-images", async (
            Guid id,
            HttpRequest httpRequest,
            [FromForm(Name = "data")] string? data,
            [FromForm(Name = "images")] List<IFormFile>? images,
            [FromForm(Name = "removeImageIds")] List<Guid>? removeImageIds,
            ILogger<UpdateProductEndpoint> logger,
            ISender sender) =>
        {
            if (string.IsNullOrWhiteSpace(data) && httpRequest.HasFormContentType)
            {
                var form = await httpRequest.ReadFormAsync();
                data = form["data"].FirstOrDefault();
            }

            if ((images is null || images.Count == 0) && httpRequest.HasFormContentType)
            {
                var form = await httpRequest.ReadFormAsync();
                images = form.Files.GetFiles("images").ToList();

                if ((removeImageIds is null || removeImageIds.Count == 0)
                    && form.TryGetValue("removeImageIds", out var removeIdsValues))
                {
                    removeImageIds = removeIdsValues
                        .Where(x => Guid.TryParse(x, out _))
                        .Select(Guid.Parse)
                        .ToList();
                }
            }

            logger.LogInformation(
                "UpdateProductWithImages request for {ProductId}: imageCount={ImageCount}, imageNames=[{ImageNames}]",
                id,
                images?.Count ?? 0,
                string.Join(", ", (images ?? []).Select(x => $"{x.FileName} ({x.Length} bytes)")));

            UpdateProductRequest? request = null;

            if (!string.IsNullOrWhiteSpace(data))
            {
                request = JsonSerializer.Deserialize<UpdateProductRequest>(data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            request ??= new UpdateProductRequest(null, null, null, null, null, null, null);

            var command = new UpdateProductCommand(id, request, images ?? [], removeImageIds ?? []);
            var result = await sender.Send(command);
            var response = result.Adapt<UpdateProductResponse>();
            return Results.Ok(response);
        })
        .WithName("UpdateProductWithImages")
        .Produces<UpdateProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Product with Images")
        .WithDescription("Update product details with file uploads via multipart/form-data. Send product data as JSON in 'data' field, new images in 'images' field, and image IDs to remove in 'removeImageIds' field.")
        .WithTags("Products")
        .DisableAntiforgery();
    }
}
