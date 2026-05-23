using Core.Caching;
using ProductCatalog.Data;

namespace ProductCatalog.Products.Features.Featured;

// TODO: Seperate this later
// ─── Toggle Featured ───────────────────────────────────────────────────────

public record ToggleFeaturedCommand(Guid ProductId, bool IsFeatured) : IRequest<ToggleFeaturedResult>;
public record ToggleFeaturedResult(Guid ProductId, bool IsFeatured);

internal class ToggleFeaturedHandler(ProductCatalogDbContext db, ICacheInvalidator cacheInvalidator)
    : IRequestHandler<ToggleFeaturedCommand, ToggleFeaturedResult>
{
    public async Task<ToggleFeaturedResult> Handle(ToggleFeaturedCommand cmd, CancellationToken ct)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == cmd.ProductId && !p.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Product {cmd.ProductId} not found");

        product.IsFeatured = cmd.IsFeatured;
        await db.SaveChangesAsync(ct);

        // Bust cache so homepage reflects the change immediately
        cacheInvalidator.Invalidate(CacheKeys.Products);

        return new ToggleFeaturedResult(product.Id, product.IsFeatured);
    }
}

// ─── Endpoints ─────────────────────────────────────────────────────────────

public class FeaturedProductsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // PATCH /api/products/{id}/featured — Admin only
        app.MapPatch("/api/products/{id:guid}/featured", async (
            Guid id,
            FeaturedToggleRequest body,
            ISender sender) =>
        {
            var result = await sender.Send(new ToggleFeaturedCommand(id, body.IsFeatured));
            return Results.Ok(result);
        })
        .WithName("ToggleProductFeatured")
        .RequireAuthorization("Admin")
        .Produces<ToggleFeaturedResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Toggle featured status of a product (admin only)")
        .WithTags("Products");
    }
}

public record FeaturedToggleRequest(bool IsFeatured);
