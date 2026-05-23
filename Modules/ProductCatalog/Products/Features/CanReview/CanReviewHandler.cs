using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;
using Contracts;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProductCatalog.Products.Features.CanReview;

public record CanReviewQuery(Guid ProductId, ClaimsPrincipal Principal) : IRequest<CanReviewResult>;

public record CanReviewResult(bool CanReview);

internal class CanReviewHandler(
    ISender sender,
    ProductCatalogDbContext catalogDb)
    : IRequestHandler<CanReviewQuery, CanReviewResult>
{
    public async Task<CanReviewResult> Handle(CanReviewQuery request, CancellationToken ct)
    {
        var externalUserId = request.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(externalUserId))
            return new CanReviewResult(false);

        // Find customer profile ID via Customer Module
        var customerId = await sender.Send(new GetCustomerByExternalIdQuery(externalUserId), ct);
        if (customerId is null)
            return new CanReviewResult(false);

        // 1. Verify that customer has a delivered order for this product via Order Module
        var hasDeliveredOrder = await sender.Send(new VerifyBuyerQuery(customerId.Value, request.ProductId), ct);
        if (!hasDeliveredOrder)
            return new CanReviewResult(false);

        // 2. Verify that customer has NOT already reviewed this product in catalog database
        var hasAlreadyReviewed = await catalogDb.ProductReviews
            .AsNoTracking()
            .AnyAsync(r => r.ProductId == request.ProductId && r.CustomerId == customerId.Value, ct);

        return new CanReviewResult(!hasAlreadyReviewed);
    }
}
