
namespace ProductCatalog.Features.Brands.UpdateBrand;

public record UpdateBrandCommand(Guid Id, UpdateBrandRequest Request) : ICommand<CreateBrandResult>;

public record UpdateBrandRequest(
    string? Name,
    string? UrlSlug,
    string? Description,
    string? LogoUrl
);

public class UpdateBrandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/brands/{id:guid}", async (Guid id, UpdateBrandRequest request, ISender sender) =>
        {
            var command = new UpdateBrandCommand(id, request);
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateBrand")
        .Produces<CreateBrandResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Brand")
        .WithDescription("Update brand details")
        .WithTags("Brands")
        .RequireAuthorization("Staff");
    }
}
