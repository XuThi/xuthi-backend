using System.Text.Json;

namespace ProductCatalog.Features.UpdateProduct;

public class UpdateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // JSON endpoint for simple updates (no file uploads)
        app.MapPut("/api/products/{id:guid}", async (Guid id, UpdateProductRequest request, ISender sender) =>
        {
            var command = new UpdateProductCommand(id, request);
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateProduct")
        .Produces<UpdateProductResult>(StatusCodes.Status200OK)
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
            var form = await httpRequest.ReadFormAsync();
            
            // Parse JSON data from form field
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
            return Results.Ok(result);
        })
        .WithName("UpdateProductWithImages")
        .Produces<UpdateProductResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Product with Images")
        .WithDescription("Update product details with file uploads via multipart/form-data. Send product data as JSON in 'data' field, new images in 'images' field, and image IDs to remove in 'removeImageIds' field.")
        .WithTags("Products")
        .DisableAntiforgery();
    }
}
