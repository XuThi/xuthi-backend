namespace ProductCatalog.VariantOptions.Features.DeleteVariantOption;

public class DeleteVariantOptionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/variant-options/{id}", async (string id, ISender sender) =>
        {
            await sender.Send(new DeleteVariantOptionCommand(id));
            return Results.NoContent();
        })
        .WithName("DeleteVariantOption")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Variant Option")
        .WithDescription("Delete a variant option definition")
        .WithTags("VariantOptions")
        .RequireAuthorization("Admin");
    }
}
