namespace ProductCatalog.Features.Groups.GetGroups;

public record GetGroupsQuery(int Page = 1, int PageSize = 20) : IQuery<GroupsResult>;

public record GroupsResult(
    List<GroupResult> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record GroupResult(
    Guid Id,
    string Name,
    int ProductCount
);

internal class GetGroupsHandler(ProductCatalogDbContext dbContext)
    : IQueryHandler<GetGroupsQuery, GroupsResult>
{
    public async Task<GroupsResult> Handle(GetGroupsQuery query, CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.Groups.CountAsync(cancellationToken);

        var groups = await dbContext.Groups
            .Include(g => g.Products)
            .OrderBy(g => g.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new GroupsResult(
            groups.Select(g => new GroupResult(g.Id, g.Name, g.Products.Count)).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );
    }
}
