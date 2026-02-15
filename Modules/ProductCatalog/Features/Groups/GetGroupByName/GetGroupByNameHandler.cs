namespace ProductCatalog.Features.Groups.GetGroupByName;

public record GetGroupByNameQuery(string Name) : IQuery<GroupDetailResult>;

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

internal class GetGroupByNameHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetGroupByNameQuery, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(GetGroupByNameQuery query, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Name == query.Name, cancellationToken);

        if (group is null)
            throw new KeyNotFoundException("Group not found");

        return new GroupDetailResult(
            group.Id,
            group.Name,
            group.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}
