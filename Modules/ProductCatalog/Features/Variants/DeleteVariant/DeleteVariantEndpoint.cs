namespace ProductCatalog.Features.Variants.DeleteVariant;

public record DeleteVariantRouteRequest(Guid VariantId);
public record DeleteVariantResponse(bool Success);

public class DeleteVariantEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/variants/{variantId:guid}", async (
            [AsParameters] DeleteVariantRouteRequest route,
            ISender sender) =>
        {
            var result = await sender.Send(new DeleteVariantCommand(route.VariantId));
            var response = new DeleteVariantResponse(result);
            return result ? Results.Ok(response) : Results.NotFound();
        })
        .WithName("DeleteVariant")
        .Produces<DeleteVariantResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Variant")
        .WithDescription("Soft delete a variant")
        .WithTags("Variants");
    }
}
