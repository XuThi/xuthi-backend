using Mapster;

namespace ProductCatalog.Brands.Features.CreateBrand;

public record CreateBrandRequest(string Name, string UrlSlug, string? Description, string? LogoUrl);
public record CreateBrandResponse(Guid Id, string Name, string UrlSlug, string? Description, string? LogoUrl);

public class CreateBrandEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/brands", async (CreateBrandRequest request, ISender sender) =>
        {
            var command = request.Adapt<CreateBrandCommand>();
            var result = await sender.Send(command);
            var response = result.Adapt<CreateBrandResponse>();
            return Results.Created($"/api/brands/{result.Id}", response);
        })
        .WithName("CreateBrand")
        .Produces<CreateBrandResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Brand")
        .WithDescription("Create a new brand")
        .WithTags("Brands")
        .RequireAuthorization("Staff");
    }
}
