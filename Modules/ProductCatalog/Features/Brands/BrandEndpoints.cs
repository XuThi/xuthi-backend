namespace ProductCatalog.Features.Brands;

public class BrandEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/brands", async (CreateBrandRequest request, ISender sender) =>
        {
            var command = new CreateBrandCommand(request);
            var result = await sender.Send(command);
            return Results.Created($"/api/brands/{result.Id}", result);
        })
        .WithName("CreateBrand")
        .Produces<BrandResult>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Brand")
        .WithDescription("Create a new brand")
        .WithTags("Brands")
        .RequireAuthorization("Staff");

        app.MapPut("/api/brands/{id:guid}", async (Guid id, UpdateBrandRequest request, ISender sender) =>
        {
            var command = new UpdateBrandCommand(id, request);
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateBrand")
        .Produces<BrandResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Brand")
        .WithDescription("Update brand details")
        .WithTags("Brands")
        .RequireAuthorization("Staff");

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
