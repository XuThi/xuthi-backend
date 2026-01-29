namespace ProductCatalog.Features.CreateProduct;

public record CreateProductResponse(Guid Id);

public class CreateProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/products", async (CreateProductRequest request, ISender sender) =>
        {
            var command = new CreateProductCommand(request);

            var result = await sender.Send(command);

            var response = new CreateProductResponse(result.Id);

            return Results.Created($"/api/products/{response.Id}", response);
        })
        .WithName("CreateProduct")
        .Produces<CreateProductResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Product")
        .WithDescription("Create a new product in the catalog")
        .WithTags("Products");
    }
}