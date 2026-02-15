namespace ProductCatalog.Features.Products.DeleteProduct;

public record DeleteProductResponse(bool Success);

public class DeleteProductEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/products/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteProductCommand(id);
            var result = await sender.Send(command);
            var response = new DeleteProductResponse(result);
            return Results.Ok(response);
        })
        .WithName("DeleteProduct")
        .Produces<DeleteProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Product")
        .WithDescription("Soft delete a product and all its variants")
        .WithTags("Products")
        .RequireAuthorization("Admin"); // Only Admin can delete
    }
}
