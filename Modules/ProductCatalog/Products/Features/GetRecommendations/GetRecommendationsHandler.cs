using ProductCatalog.Data;
using Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProductCatalog.Products.Features.GetRecommendations;

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
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "giày", "cao", "gót", "nữ", "màu", "với", "cho", "và", "của", "phong", "cách", "thiết", "kế", "đẹp", "sang", "trọng", "thanh", "lịch", "hiện", "đại", "thương", "hiệu", "các", "mẫu", "độc", "đáo", "sự", "kết", "hợp", "hoàn", "hảo", "tinh", "tế", "mang", "lại", "vẻ", "cực", "kỳ", "quai", "những", "nổi", "bật", "tự", "tin", "tôn", "dáng"
    };

    public async Task<GetRecommendationsResult> Handle(GetRecommendationsQuery query, CancellationToken ct)
    {
        // 1. Fetch current product's details
        var currentProduct = await db.Products
            .Where(p => p.Id == query.ProductId && !p.IsDeleted)
            .Select(p => new
            {
                p.CategoryId,
                p.BrandId,
                p.Name,
                p.Description,
                MinPrice = p.Variants.Where(v => !v.IsDeleted).Min(v => (decimal?)v.Price) ?? 0
            })
            .FirstOrDefaultAsync(ct);

        if (currentProduct is null)
            return new GetRecommendationsResult([]);

        var recommendedProducts = new List<RecommendedProductDto>();

        // 2. Fetch Frequently Bought Together (FBT) product IDs from delivered orders via Order Module
        var fbtProductIds = await sender.Send(new GetFrequentlyBoughtTogetherProductIdsQuery(query.ProductId, query.Limit), ct);

        if (fbtProductIds != null && fbtProductIds.Count > 0)
        {
            var fbtProducts = await db.Products
                .Where(p => fbtProductIds.Contains(p.Id) && !p.IsDeleted && p.IsActive)
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

            var fbtOrdered = fbtProducts
                .OrderBy(p => fbtProductIds.IndexOf(p.Id))
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

        // 3. Fallback: If we don't have enough recommendations, fill with similar products
        if (recommendedProducts.Count < query.Limit)
        {
            var excludeIds = recommendedProducts.Select(p => p.Id).Concat([query.ProductId]).ToList();
            var remainingCount = query.Limit - recommendedProducts.Count;

            // Fetch other active candidates in the system (since all are in the same category/brand)
            var candidates = await db.Products
                .Where(p => !excludeIds.Contains(p.Id) && !p.IsDeleted && p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
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

            // Tokenize the current product's name and description, removing stop words
            var currentKeywords = $"{currentProduct.Name} {currentProduct.Description}"
                .ToLowerInvariant()
                .Split(new[] { ' ', '-', ',', '/', '(', ')', '.', '!', '?', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1 && !StopWords.Contains(w))
                .Distinct()
                .ToList();

            var rankedCandidates = candidates.Select(p =>
            {
                // Calculate name + description keyword match score
                var targetTextLower = $"{p.Name} {p.Description}".ToLowerInvariant();
                int keywordMatches = 0;

                foreach (var kw in currentKeywords)
                {
                    if (targetTextLower.Contains(kw))
                    {
                        keywordMatches++;
                    }
                }

                var keywordScore = keywordMatches * 50.0;

                // Calculate price proximity score (maximum 100, dropping by 1 per 10k VND difference)
                var priceDiff = Math.Abs(p.MinPrice - currentProduct.MinPrice);
                var priceScore = Math.Max(0, 100 - (double)(priceDiff / 10000m));

                var totalSimilarityScore = keywordScore + priceScore;

                return new
                {
                    Product = p,
                    Score = totalSimilarityScore
                };
            })
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Product.AverageRating)
            .ThenBy(r => Guid.NewGuid()) // Tie-breaker: shuffle equal scores to keep recommendations dynamic
            .Take(remainingCount)
            .Select(r => new RecommendedProductDto(
                r.Product.Id,
                r.Product.Name,
                r.Product.UrlSlug,
                r.Product.Images!,
                r.Product.MinPrice,
                r.Product.AverageRating,
                r.Product.ReviewCount
            ))
            .ToList();

            recommendedProducts.AddRange(rankedCandidates);
        }

        return new GetRecommendationsResult(recommendedProducts);
    }
}
