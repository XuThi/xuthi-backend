namespace ProductCatalog.Features.GetCategories;

internal class GetCategoriesHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetCategoriesQuery, GetCategoriesResult>
{
    public async Task<GetCategoriesResult> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Categories.AsQueryable();

        // Filter by parent category if specified
        if (request.ParentId.HasValue)
        {
            query = query.Where(c => c.ParentCategoryId == request.ParentId.Value);
        }

        var categories = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryItem(
                c.Id,
                c.Name,
                c.UrlSlug,
                c.Description,
                c.ParentCategoryId,
                c.SortOrder,
                dbContext.Products.Count(p => p.CategoryId == c.Id && !p.IsDeleted && p.IsActive)
            ))
            .ToListAsync(cancellationToken);

        return new GetCategoriesResult(categories);
    }
}
