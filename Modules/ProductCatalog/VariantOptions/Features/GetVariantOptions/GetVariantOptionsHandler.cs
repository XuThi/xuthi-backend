namespace ProductCatalog.VariantOptions.Features.GetVariantOptions;

public record GetVariantOptionsQuery : IQuery<List<VariantOptionResult>>;
public record VariantOptionResult(string Id, string Name, string DisplayType, List<string> Values);

internal class GetVariantOptionsHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetVariantOptionsQuery, List<VariantOptionResult>>
{
    public async Task<List<VariantOptionResult>> Handle(GetVariantOptionsQuery query, CancellationToken cancellationToken)
    {
        return await dbContext.VariantOptions
            .Include(o => o.Values)
            .OrderBy(o => o.Name)
            .Select(o => new VariantOptionResult(
                o.Id,
                o.Name,
                o.DisplayType,
                o.Values.OrderBy(v => v.SortOrder).Select(v => v.Value).ToList()
            ))
            .ToListAsync(cancellationToken);
    }
}
