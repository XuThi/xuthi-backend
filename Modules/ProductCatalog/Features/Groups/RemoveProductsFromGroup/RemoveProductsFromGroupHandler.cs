namespace ProductCatalog.Features.Groups.RemoveProductsFromGroup;

public record RemoveProductsFromGroupCommand(Guid GroupId, List<Guid> ProductIds) : ICommand<GroupDetailResult>;

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

internal class RemoveProductsFromGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<RemoveProductsFromGroupCommand, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(RemoveProductsFromGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.FindAsync([command.GroupId], cancellationToken);
        if (group is null)
            throw new KeyNotFoundException("Group not found");

        var toRemove = await dbContext.GroupProducts
            .Where(gp => gp.GroupId == command.GroupId && command.ProductIds.Contains(gp.ProductId))
            .ToListAsync(cancellationToken);

        dbContext.GroupProducts.RemoveRange(toRemove);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await dbContext.Groups
            .Include(g => g.Products)
            .FirstAsync(g => g.Id == command.GroupId, cancellationToken);

        return new GroupDetailResult(
            updated.Id,
            updated.Name,
            updated.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}
