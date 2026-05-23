using Core.Caching;
using ProductCatalog.Data;
using ProductCatalog.Products.Models;
using Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProductCatalog.Products.Features.Reviews;

// TODO: This file is getting large - consider splitting into separate files for Queries, Commands, and Handlers for better organization and maintainability.

// ─── DTOs ────────────────────────────────────────────────────────────────────

public record ProductReviewDto(
    Guid Id,
    string AuthorName,
    int Rating,
    string? Comment,
    DateTime CreatedAt
);

// ─── Submit Review ─────────────────────────────────────────────────────────

public record SubmitReviewCommand(
    Guid ProductId,
    string AuthorName,
    string AuthorEmail,
    int Rating,
    string? Comment,
    Guid? CustomerId = null
) : IRequest<SubmitReviewResult>;

public record SubmitReviewResult(Guid ReviewId, decimal NewAverageRating, int NewReviewCount);

public class SubmitReviewValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewValidator()
    {
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AuthorEmail).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(2000).When(x => x.Comment is not null);
    }
}

internal class SubmitReviewHandler(
    ProductCatalogDbContext db,
    ISender sender,
    ICacheInvalidator cacheInvalidator,
    IConfiguration configuration,
    HttpClient httpClient)
    : IRequestHandler<SubmitReviewCommand, SubmitReviewResult>
{
    public async Task<SubmitReviewResult> Handle(SubmitReviewCommand cmd, CancellationToken ct)
    {
        // Verified Buyer Check
        if (!cmd.CustomerId.HasValue)
        {
            throw new InvalidOperationException("Chỉ khách hàng đã đăng nhập mới có thể viết đánh giá.");
        }

        // Verify that customer has a delivered order for this product via Order Module
        var hasDeliveredOrder = await sender.Send(new VerifyBuyerQuery(cmd.CustomerId.Value, cmd.ProductId), ct);

        if (!hasDeliveredOrder)
        {
            throw new InvalidOperationException("Chỉ những khách hàng đã mua sản phẩm này mới có thể viết đánh giá.");
        }

        // Verify that customer has NOT already reviewed this product
        var hasAlreadyReviewed = await db.ProductReviews
            .AsNoTracking()
            .AnyAsync(r => r.ProductId == cmd.ProductId && r.CustomerId == cmd.CustomerId.Value, ct);

        if (hasAlreadyReviewed)
        {
            throw new InvalidOperationException("Bạn đã đánh giá sản phẩm này rồi.");
        }

        var product = await db.Products
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == cmd.ProductId && !p.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Product {cmd.ProductId} not found");

        var review = new ProductReview
        {
            Id = Guid.NewGuid(),
            ProductId = cmd.ProductId,
            CustomerId = cmd.CustomerId,
            AuthorName = cmd.AuthorName,
            AuthorEmail = cmd.AuthorEmail,
            Rating = cmd.Rating,
            Comment = cmd.Comment,
            IsApproved = true,
        };

        db.ProductReviews.Add(review);

        // EF Core navigation fixup automatically adds the tracked review to product.Reviews.
        // Recalculate denormalized stats on the product row itself so reads never need a JOIN.
        var approvedReviews = product.Reviews.Where(r => r.IsApproved).ToList();
        product.AverageRating = approvedReviews.Count > 0
            ? Math.Round((decimal)approvedReviews.Average(r => r.Rating), 2)
            : 0;
        product.ReviewCount = approvedReviews.Count;

        await db.SaveChangesAsync(ct);

        // Invalidate cached product searches so the updated rating shows up immediately
        cacheInvalidator.Invalidate(CacheKeys.Products);

        // Trigger frontend cache revalidation to purge Next.js `"use cache"` layer
        _ = Task.Run(async () =>
        {
            try
            {
                var frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:3000";
                // Note: The frontend revalidation route /api/revalidate/catalog checks for Admin/Staff role
                // via backend /api/auth/me using the Authorization header.
                // In a production scenario, we'd use a shared secret key instead of user-based auth for background tasks.
                // Since this is a simple store, we'll hit the endpoint. If it's blocked by auth, 
                // the frontend will still update when the cache expires (minutes).
                await httpClient.PostAsync($"{frontendUrl}/api/revalidate/catalog", null);
            }
            catch
            {
                // Background task - ignore failures to prevent blocking the response
            }
        }, CancellationToken.None);

        return new SubmitReviewResult(review.Id, product.AverageRating, product.ReviewCount);
    }
}

// ─── Get Reviews ───────────────────────────────────────────────────────────

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

// ─── Get Recommendations ───────────────────────────────────────────────────

public record GetRecommendationsQuery(Guid ProductId, int Limit = 4) : IRequest<GetRecommendationsResult>;
public record GetRecommendationsResult(List<RecommendedProductDto> Products);

public record RecommendedProductDto(
    Guid Id,
    string Name,
    string Slug,
    List<string> Images,
    decimal MinPrice,
    decimal AverageRating,
    int ReviewCount
);

internal class GetRecommendationsHandler(ProductCatalogDbContext db, ISender sender)
    : IRequestHandler<GetRecommendationsQuery, GetRecommendationsResult>
{
    public async Task<GetRecommendationsResult> Handle(GetRecommendationsQuery query, CancellationToken ct)
    {
        // Find the category of the current product
        var currentProduct = await db.Products
            .Where(p => p.Id == query.ProductId && !p.IsDeleted)
            .Select(p => new { p.CategoryId })
            .FirstOrDefaultAsync(ct);

        if (currentProduct is null)
            return new GetRecommendationsResult([]);

        // 1. Fetch "Frequently Bought Together" product IDs from delivered orders via Order Module
        var recommendedProductIds = await sender.Send(new GetFrequentlyBoughtTogetherProductIdsQuery(query.ProductId, query.Limit), ct);

        // 2. Fetch the corresponding product details for the recommended IDs
        var recommendedProducts = new List<RecommendedProductDto>();
        if (recommendedProductIds != null && recommendedProductIds.Count > 0)
        {
            var fbtProducts = await db.Products
                .Where(p => recommendedProductIds.Contains(p.Id) && !p.IsDeleted && p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.UrlSlug,
                    p.AverageRating,
                    p.ReviewCount,
                    Images = p.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.Image!.Url)
                        .Where(u => u != null)
                        .ToList(),
                    MinPrice = p.Variants
                        .Where(v => !v.IsDeleted)
                        .Min(v => (decimal?)v.Price) ?? 0
                })
                .ToListAsync(ct);

            // Re-sort to maintain order of frequency descending
            var fbtOrdered = fbtProducts
                .OrderBy(p => recommendedProductIds.IndexOf(p.Id))
                .Select(p => new RecommendedProductDto(
                    p.Id,
                    p.Name,
                    p.UrlSlug,
                    p.Images!,
                    p.MinPrice,
                    p.AverageRating,
                    p.ReviewCount
                )).ToList();

            recommendedProducts.AddRange(fbtOrdered);
        }

        // 3. Fallback: If we don't have enough recommendations, fill with top products in the same category
        if (recommendedProducts.Count < query.Limit)
        {
            var excludeIds = recommendedProducts.Select(p => p.Id).Concat([query.ProductId]).ToList();
            var remainingCount = query.Limit - recommendedProducts.Count;

            var fallbackProducts = await db.Products
                .Where(p => p.CategoryId == currentProduct.CategoryId
                            && !excludeIds.Contains(p.Id)
                            && !p.IsDeleted
                            && p.IsActive)
                .OrderByDescending(p => p.AverageRating)
                .ThenByDescending(p => p.ReviewCount)
                .Take(remainingCount)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.UrlSlug,
                    p.AverageRating,
                    p.ReviewCount,
                    Images = p.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => i.Image!.Url)
                        .Where(u => u != null)
                        .ToList(),
                    MinPrice = p.Variants
                        .Where(v => !v.IsDeleted)
                        .Min(v => (decimal?)v.Price) ?? 0
                })
                .ToListAsync(ct);

            var fallbackDtos = fallbackProducts.Select(p => new RecommendedProductDto(
                p.Id,
                p.Name,
                p.UrlSlug,
                p.Images!,
                p.MinPrice,
                p.AverageRating,
                p.ReviewCount
            )).ToList();

            recommendedProducts.AddRange(fallbackDtos);
        }

        return new GetRecommendationsResult(recommendedProducts);
    }
}
