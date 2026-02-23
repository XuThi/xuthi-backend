using System.Text.Json;
using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace ProductCatalog.Products.Features.CreateProduct;

public record CreateProductResponse(Guid Id, List<Guid> VariantIds, List<string> ImageUrls);

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (
            [FromForm(Name = "data")] string? data,
            [FromForm(Name = "images")] List<IFormFile>? images,
            ISender sender) =>
        {
            CreateProductRequest? request = null;

            if (!string.IsNullOrWhiteSpace(data))
            {
                request = JsonSerializer.Deserialize<CreateProductRequest>(data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            if (request == null)
            {
                return Results.BadRequest("Invalid product data. Send multipart/form-data with 'data' field.");
            }

            var command = new CreateProductCommand(request, images ?? []);
            var result = await sender.Send(command);
            var response = result.Adapt<CreateProductResponse>();
            return Results.Created($"/api/products/{result.Id}", response);
        })
        .WithName("CreateProduct")
        .Produces<CreateProductResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Product")
        .WithDescription("Create a new product via multipart/form-data. Send JSON in 'data' field and files in 'images' field.")
        .WithTags("Products")
        .DisableAntiforgery();
    }
}
