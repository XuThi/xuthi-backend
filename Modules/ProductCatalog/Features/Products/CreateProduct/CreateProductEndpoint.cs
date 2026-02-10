using System.Text.Json;

namespace ProductCatalog.Features.Products.CreateProduct;

// TODO: I will fuck this shit up later

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (HttpRequest httpRequest, ISender sender) =>
        {
            CreateProductRequest? request = null;
            List<IFormFile> images = [];

            if (httpRequest.HasFormContentType)
            {
                var form = await httpRequest.ReadFormAsync();
                
                // Parse JSON body from form field
                var jsonData = form["data"].FirstOrDefault();
                if (!string.IsNullOrEmpty(jsonData))
                {
                    request = JsonSerializer.Deserialize<CreateProductRequest>(jsonData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                
                images = form.Files.GetFiles("images").ToList();
            }
            else
            {
                // Try JSON body
                try 
                {
                    request = await httpRequest.ReadFromJsonAsync<CreateProductRequest>();
                }
                catch (JsonException)
                {
                    // Ignore, request will be null
                }
            }

            if (request == null)
            {
                return Results.BadRequest("Invalid product data. Send JSON body or multipart/form-data with 'data' field.");
            }

            var command = new CreateProductCommand(request, images);
            var result = await sender.Send(command);
            return Results.Created($"/api/products/{result.Id}", result);
        })
        .WithName("CreateProduct")
        .Produces<CreateProductResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Product")
        .WithDescription("Create a new product. Supports both 'application/json' and 'multipart/form-data' (for images). For multipart, send JSON in 'data' field and files in 'images' field.")
        .WithTags("Products")
        .DisableAntiforgery();
    }
}
