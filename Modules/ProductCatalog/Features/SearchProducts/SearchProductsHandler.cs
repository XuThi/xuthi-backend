namespace ProductCatalog.Features.SearchProducts;

internal class SearchProductsHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<SearchProductsQuery, SearchProductsResult>
{
    public async Task<SearchProductsResult> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;

        // Start with base query - include all related data
        var query = dbContext.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .Include(p => p.Variants)
                .ThenInclude(v => v.OptionSelections)
            .AsQueryable();

        // Apply filters
        query = ApplyFilters(query, req);

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        query = ApplySorting(query, req.SortBy, req.SortDescending);

        // Apply pagination
        var page = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        query = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        // Execute query
        var productEntities = await query.ToListAsync(cancellationToken);

        // Map to result format matching frontend types
        var products = productEntities.Select(p => new ProductSearchItem(
            Id: p.Id,
            Name: p.Name,
            Slug: p.UrlSlug, // Frontend expects 'slug'
            Summary: null, // We don't have a summary field separately
            Description: p.Description,
            Images: p.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.Image.Url)
                .ToList(),
            CategoryId: p.CategoryId,
            CategoryName: p.Category?.Name,
            BrandId: p.BrandId,
            BrandName: p.Brand?.Name,
            IsActive: p.IsActive,
            Variants: p.Variants
                .Where(v => !v.IsDeleted)
                .Select(v => new ProductVariantItem(
                    Id: v.Id,
                    Sku: v.Sku,
                    Name: v.Description,
                    Price: v.Price,
                    CompareAtPrice: null, // Simplified - no compare price
                    StockQuantity: 100, // Simplified - always show as available
                    Images: [], // Variant-specific images not loaded here
                    Attributes: v.OptionSelections
                        .ToDictionary(os => os.VariantOptionId, os => os.Value)
                ))
                .ToList(),
            CreatedAt: p.CreatedAt,
            UpdatedAt: p.UpdatedAt
        )).ToList();

        return new SearchProductsResult(
            products,
            totalCount,
            page,
            pageSize,
            totalPages
        );
    }

    private static IQueryable<Product> ApplyFilters(IQueryable<Product> query, SearchProductsRequest req)
    {
        // Text search on name and description
        if (!string.IsNullOrWhiteSpace(req.Query))
        {
            var searchTerm = req.Query.ToLower().Trim();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchTerm) ||
                p.Description.ToLower().Contains(searchTerm) ||
                p.UrlSlug.ToLower().Contains(searchTerm)
            );
        }

        // Filter by category
        if (req.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == req.CategoryId.Value);
        }

        // Filter by brand
        if (req.BrandId.HasValue)
        {
            query = query.Where(p => p.BrandId == req.BrandId.Value);
        }

        // Filter by active status
        if (req.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == req.IsActive.Value);
        }

        // Exclude deleted products
        query = query.Where(p => !p.IsDeleted);

        // Filter by price range (based on variant prices)
        if (req.MinPrice.HasValue)
        {
            query = query.Where(p => p.Variants.Any(v => !v.IsDeleted && v.Price >= req.MinPrice.Value));
        }

        if (req.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Variants.Any(v => !v.IsDeleted && v.Price <= req.MaxPrice.Value));
        }

        return query;
    }

    private static IQueryable<Product> ApplySorting(IQueryable<Product> query, string? sortBy, bool descending)
    {
        return sortBy?.ToLower() switch
        {
            "name" => descending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name),

            "createdat" or "created" => descending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),

            "updatedat" or "updated" => descending
                ? query.OrderByDescending(p => p.UpdatedAt)
                : query.OrderBy(p => p.UpdatedAt),

            "price" => descending
                ? query.OrderByDescending(p => p.Variants.Where(v => !v.IsDeleted).Any() ? p.Variants.Where(v => !v.IsDeleted).Min(v => v.Price) : decimal.MaxValue)
                : query.OrderBy(p => p.Variants.Where(v => !v.IsDeleted).Any() ? p.Variants.Where(v => !v.IsDeleted).Min(v => v.Price) : decimal.MaxValue),

            _ => query.OrderByDescending(p => p.CreatedAt) // Default: newest first
        };
    }
}
