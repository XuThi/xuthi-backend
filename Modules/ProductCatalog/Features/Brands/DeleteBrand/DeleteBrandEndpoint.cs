
namespace ProductCatalog.Features.Brands.DeleteBrand;

public record DeleteBrandCommand(Guid Id) : ICommand<bool>;
public record DeleteBrandResponse(bool isSuccess);

public class DeleteBrandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/brands/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteBrandCommand(id);
            await sender.Send(command);
            return Results.NoContent();
        })
        .WithName("DeleteBrand")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Brand")
        .WithDescription("Delete a brand (must have no products)")
        .WithTags("Brands")
        .RequireAuthorization("Admin");
    }
}