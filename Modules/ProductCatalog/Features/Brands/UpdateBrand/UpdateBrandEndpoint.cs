using Mapster;

namespace ProductCatalog.Features.Brands.UpdateBrand;

public record UpdateBrandRouteRequest(Guid Id);
public record UpdateBrandResponse(Guid Id, string Name, string UrlSlug, string? Description, string? LogoUrl);

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
        app.MapPut("/api/brands/{id:guid}", async (
            [AsParameters] UpdateBrandRouteRequest route,
            UpdateBrandRequest request,
            ISender sender) =>
        {
            var command = new UpdateBrandCommand(route.Id, request);
            var result = await sender.Send(command);
            var response = result.Adapt<UpdateBrandResponse>();
            return Results.Ok(response);
        })
        .WithName("UpdateBrand")
        .Produces<UpdateBrandResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Brand")
        .WithDescription("Update brand details")
        .WithTags("Brands")
        .RequireAuthorization("Staff");
    }
}
