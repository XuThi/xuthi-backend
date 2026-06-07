using Core.Caching;
using ProductCatalog.Data;
using ProductCatalog.Products.Models;
using Contracts;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProductCatalog.Products.Features.SubmitReview;

public record SubmitReviewCommand(
    Guid ProductId,
    string AuthorName,
    string AuthorEmail,
    int Rating,
    string? Comment,
    Guid? CustomerId = null
) : IRequest<SubmitReviewResult>;

public record SubmitReviewResult(Guid ReviewId, decimal NewAverageRating, int NewReviewCount);

public class SubmitReviewCommandValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewCommandValidator()
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

        // Recalculate average rating and review count on the product row
        var approvedReviews = product.Reviews.Where(r => r.IsApproved).ToList();
        product.AverageRating = approvedReviews.Count > 0
            ? Math.Round((decimal)approvedReviews.Average(r => r.Rating), 2)
            : 0;
        product.ReviewCount = approvedReviews.Count;

        await db.SaveChangesAsync(ct);

        // Invalidate cached product searches so the updated rating shows up immediately
        cacheInvalidator.Invalidate(CacheKeys.Products);

        // Trigger frontend cache revalidation
        _ = Task.Run(async () =>
        {
            try
            {
                var frontendUrl = configuration["FrontendUrl"];
                await httpClient.PostAsync($"{frontendUrl}/api/revalidate/catalog", null);
            }
            catch
            {
                // Ignore failure in background task
            }
        }, CancellationToken.None);

        return new SubmitReviewResult(review.Id, product.AverageRating, product.ReviewCount);
    }
}
