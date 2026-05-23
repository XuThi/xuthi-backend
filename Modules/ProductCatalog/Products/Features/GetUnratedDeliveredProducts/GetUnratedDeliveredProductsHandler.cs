using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Data;
using Contracts;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProductCatalog.Products.Features.GetUnratedDeliveredProducts;

public record GetUnratedDeliveredProductsQuery(ClaimsPrincipal Principal) : IRequest<UnratedProductsResult>;

public record UnratedProductsResult(List<UnratedProductDto> Products);

public record UnratedProductDto(
    Guid Id,
    string Name,
    string Slug,
    string? ImageUrl
);

internal class GetUnratedDeliveredProductsHandler(
    ISender sender,
    ProductCatalogDbContext catalogDb)
    : IRequestHandler<GetUnratedDeliveredProductsQuery, UnratedProductsResult>
{
    public async Task<UnratedProductsResult> Handle(GetUnratedDeliveredProductsQuery request, CancellationToken ct)
    {
        var externalUserId = request.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(externalUserId))
            return new UnratedProductsResult([]);

        // Find customer profile ID via Customer Module
        var customerId = await sender.Send(new GetCustomerByExternalIdQuery(externalUserId), ct);
        if (customerId is null)
            return new UnratedProductsResult([]);

        // 1. Fetch all product IDs from DELIVERED orders via Order Module
        var deliveredProductIds = await sender.Send(new GetDeliveredProductIdsQuery(customerId.Value), ct);
        if (deliveredProductIds == null || deliveredProductIds.Count == 0)
            return new UnratedProductsResult([]);

        // 2. Fetch all product IDs already reviewed by this customer in catalog database
        var reviewedProductIds = await catalogDb.ProductReviews
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId.Value)
            .Select(r => r.ProductId)
            .ToListAsync(ct);

        // 3. Find delivered product IDs that are UNRATED
        var unratedProductIds = deliveredProductIds.Except(reviewedProductIds).ToList();
        if (unratedProductIds.Count == 0)
            return new UnratedProductsResult([]);

        // 4. Load product details for unrated product IDs
        var products = await catalogDb.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .ThenInclude(pi => pi.Image)
            .Where(p => unratedProductIds.Contains(p.Id) && !p.IsDeleted)
            .Select(p => new UnratedProductDto(
                p.Id,
                p.Name,
                p.UrlSlug,
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.Image!.Url).FirstOrDefault()
            ))
            .ToListAsync(ct);

        return new UnratedProductsResult(products);
    }
}
