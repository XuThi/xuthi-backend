namespace ProductCatalog.Groups.Features.GetGroup;

public record GetGroupQuery(Guid Id) : IQuery<GroupDetailResult>;

public record GroupDetailResult(
    Guid Id,
    string Name,
    List<GroupProductInfo> Products
);

public record GroupProductInfo(
    Guid ProductId,
    string ProductName,
    string? UrlSlug
);

internal class GetGroupHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetGroupQuery, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(GetGroupQuery query, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == query.Id, cancellationToken);

        if (group is null)
            throw new KeyNotFoundException("Group not found");

        return new GroupDetailResult(
            group.Id,
            group.Name,
            group.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}
