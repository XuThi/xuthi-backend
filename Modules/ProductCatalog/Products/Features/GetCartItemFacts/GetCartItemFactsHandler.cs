namespace ProductCatalog.Products.Features.GetCartItemFacts;

public record GetCartItemFactsQuery(List<Guid> VariantIds) : IQuery<GetCartItemFactsResult>;

public record GetCartItemFactsResult(List<CartItemFact> Items);

public record CartItemFact(
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantSku,
    string? VariantDescription,
    string? ImageUrl,
    decimal BasePrice,
    decimal? CompareAtPrice,
    int StockQuantity,
    bool IsAvailable,
    Guid CategoryId);

internal class GetCartItemFactsHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetCartItemFactsQuery, GetCartItemFactsResult>
{
    public async Task<GetCartItemFactsResult> Handle(GetCartItemFactsQuery query, CancellationToken ct)
    {
        var variantIds = query.VariantIds.Distinct().ToList();
        if (variantIds.Count == 0)
            return new GetCartItemFactsResult([]);

        var variants = await dbContext.Variants
            .Include(v => v.OptionSelections)
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(ct);

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var products = await dbContext.Products
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var optionIds = variants
            .SelectMany(v => v.OptionSelections.Select(os => os.VariantOptionId))
            .Distinct()
            .ToList();

        var optionNameMap = optionIds.Count == 0
            ? new Dictionary<string, string>()
            : await dbContext.VariantOptions
                .Where(vo => optionIds.Contains(vo.Id))
                .ToDictionaryAsync(vo => vo.Id, vo => vo.Name, ct);

        var items = new List<CartItemFact>();

        foreach (var variant in variants)
        {
            if (!products.TryGetValue(variant.ProductId, out var product))
                continue;

            var variantDescription = string.Join(", ", variant.OptionSelections.Select(os =>
            {
                var name = optionNameMap.TryGetValue(os.VariantOptionId, out var optionName)
                    ? optionName
                    : os.VariantOptionId;
                return $"{name}: {os.Value}";
            }));

            items.Add(new CartItemFact(
                ProductId: product.Id,
                VariantId: variant.Id,
                ProductName: product.Name,
                VariantSku: variant.Sku,
                VariantDescription: string.IsNullOrWhiteSpace(variantDescription) ? null : variantDescription,
                ImageUrl: product.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Image.Url,
                BasePrice: variant.Price,
                CompareAtPrice: variant.CompareAtPrice,
                StockQuantity: variant.StockQuantity,
                IsAvailable: product.IsActive && !product.IsDeleted && variant.IsActive && !variant.IsDeleted,
                CategoryId: product.CategoryId));
        }

        return new GetCartItemFactsResult(items);
    }
}
