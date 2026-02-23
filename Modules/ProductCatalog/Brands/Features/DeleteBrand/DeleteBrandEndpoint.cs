
namespace ProductCatalog.Brands.Features.DeleteBrand;

public record DeleteBrandResponse(bool isSuccess);

public class DeleteBrandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/brands/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteBrandCommand(id);
            var result = await sender.Send(command);
            var response = new DeleteBrandResponse(result);
            return Results.Ok(response);
        })
        .WithName("DeleteBrand")
        .Produces<DeleteBrandResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Brand")
        .WithDescription("Delete a brand (must have no products)")
        .WithTags("Brands")
        .RequireAuthorization("Admin");
    }
}
