using ProductCatalog.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProductCatalog.Products.Features.GetReviews;

public record ProductReviewDto(
    Guid Id,
    string AuthorName,
    int Rating,
    string? Comment,
    DateTime CreatedAt
);

public record GetReviewsQuery(Guid ProductId) : IRequest<GetReviewsResult>;

public record GetReviewsResult(
    List<ProductReviewDto> Reviews,
    decimal AverageRating,
    int ReviewCount
);

internal class GetReviewsHandler(ProductCatalogDbContext db)
    : IRequestHandler<GetReviewsQuery, GetReviewsResult>
{
    public async Task<GetReviewsResult> Handle(GetReviewsQuery query, CancellationToken ct)
    {
        var reviews = await db.ProductReviews
            .Where(r => r.ProductId == query.ProductId && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ProductReviewDto(
                r.Id,
                r.AuthorName,
                r.Rating,
                r.Comment,
                r.CreatedAt ?? DateTime.UtcNow))
            .ToListAsync(ct);

        var product = await db.Products
            .Where(p => p.Id == query.ProductId)
            .Select(p => new { p.AverageRating, p.ReviewCount })
            .FirstOrDefaultAsync(ct);

        return new GetReviewsResult(
            reviews,
            product?.AverageRating ?? 0,
            product?.ReviewCount ?? 0
        );
    }
}
