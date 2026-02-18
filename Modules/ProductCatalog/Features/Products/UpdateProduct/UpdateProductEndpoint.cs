using System.Text.Json;
using Mapster;

namespace ProductCatalog.Features.Products.UpdateProduct;

// TODO: Remove the fucking try catch block here

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
            ISender sender) =>
        {
            try
            {
                var form = await httpRequest.ReadFormAsync();

                var jsonData = form["data"].FirstOrDefault();
                UpdateProductRequest? request = null;

                if (!string.IsNullOrEmpty(jsonData))
                {
                    request = JsonSerializer.Deserialize<UpdateProductRequest>(jsonData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                request ??= new UpdateProductRequest(null, null, null, null, null, null, null);

                var images = form.Files.GetFiles("images").ToList();
                var removeImageIds = form["removeImageIds"]
                    .Where(x => Guid.TryParse(x, out _))
                    .Select(x => Guid.Parse(x!))
                    .ToList();

                var command = new UpdateProductCommand(id, request, images, removeImageIds);
                var result = await sender.Send(command);
                var response = result.Adapt<UpdateProductResponse>();
                return Results.Ok(response);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { message = "Dữ liệu sản phẩm không hợp lệ." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
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
