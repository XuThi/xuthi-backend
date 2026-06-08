using Carter;
using Core.Caching;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;

namespace ProductCatalog.Products.Features.AdminReviews;

public record AdminReviewDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string AuthorName,
    string AuthorEmail,
    int Rating,
    string? Comment,
    bool IsApproved,
    DateTime CreatedAt
);

public record GetAdminReviewsQuery : IRequest<List<AdminReviewDto>>;

internal class GetAdminReviewsHandler(ProductCatalogDbContext db)
    : IRequestHandler<GetAdminReviewsQuery, List<AdminReviewDto>>
{
    public async Task<List<AdminReviewDto>> Handle(GetAdminReviewsQuery request, CancellationToken ct)
    {
        return await db.ProductReviews
            .AsNoTracking()
            .Include(r => r.Product)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AdminReviewDto(
                r.Id,
                r.ProductId,
                r.Product.Name,
                r.AuthorName,
                r.AuthorEmail,
                r.Rating,
                r.Comment,
                r.IsApproved,
                r.CreatedAt ?? DateTime.UtcNow))
            .ToListAsync(ct);
    }
}

public record DeleteReviewCommand(Guid ReviewId) : IRequest;

internal class DeleteReviewHandler(
    ProductCatalogDbContext db,
    ICacheInvalidator cacheInvalidator)
    : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand request, CancellationToken ct)
    {
        var review = await db.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, ct)
            ?? throw new KeyNotFoundException("Review not found");

        var product = await db.Products
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == review.ProductId, ct)
            ?? throw new KeyNotFoundException("Product not found");

        db.ProductReviews.Remove(review);

        var remainingApprovedReviews = product.Reviews
            .Where(r => r.Id != review.Id && r.IsApproved)
            .ToList();

        product.AverageRating = remainingApprovedReviews.Count > 0
            ? Math.Round((decimal)remainingApprovedReviews.Average(r => r.Rating), 2)
            : 0;
        product.ReviewCount = remainingApprovedReviews.Count;
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        cacheInvalidator.Invalidate(CacheKeys.Products);
    }
}

public class AdminReviewsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/reviews", async (ISender sender) =>
        {
            var reviews = await sender.Send(new GetAdminReviewsQuery());
            return Results.Ok(new { reviews });
        })
        .RequireAuthorization("Admin")
        .WithName("GetAdminReviews")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithSummary("Get reviews for moderation")
        .WithTags("Admin");

        app.MapDelete("/api/admin/reviews/{id:guid}", async (
            [FromRoute] Guid id,
            ISender sender) =>
        {
            await sender.Send(new DeleteReviewCommand(id));
            return Results.NoContent();
        })
        .RequireAuthorization("Admin")
        .WithName("DeleteAdminReview")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete a product review")
        .WithTags("Admin");
    }
}
