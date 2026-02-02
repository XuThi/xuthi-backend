using System.Text.Json;

namespace ProductCatalog.Features.CreateProduct;

// TODO: I will fuck this shit up later

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // JSON endpoint (no images)
        app.MapPost("/api/products", async (CreateProductRequest request, ISender sender) =>
        {
            var command = new CreateProductCommand(request);
            var result = await sender.Send(command);
            return Results.Created($"/api/products/{result.Id}", result);
        })
        .WithName("CreateProduct")
        .Produces<CreateProductResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Product")
        .WithDescription("Create a new product (JSON, no images)")
        .WithTags("Products");

        // Multipart form endpoint (with images)
        app.MapPost("/api/products/with-images", async (HttpRequest httpRequest, ISender sender) =>
        {
            var form = await httpRequest.ReadFormAsync();
            
            // Parse JSON body from form field
            var jsonData = form["data"].FirstOrDefault();
            if (string.IsNullOrEmpty(jsonData))
                return Results.BadRequest("Missing 'data' field with product JSON");
                
            var request = JsonSerializer.Deserialize<CreateProductRequest>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (request == null)
                return Results.BadRequest("Invalid product data");

            var images = form.Files.GetFiles("images").ToList();

            var command = new CreateProductCommand(request, images);
            var result = await sender.Send(command);
            return Results.Created($"/api/products/{result.Id}", result);
        })
        .WithName("CreateProductWithImages")
        .Produces<CreateProductResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Product with Images")
        .WithDescription("Create a new product with image uploads via multipart/form-data. Send product data as JSON in 'data' field and images in 'images' field.")
        .WithTags("Products")
        .DisableAntiforgery();
    }
}