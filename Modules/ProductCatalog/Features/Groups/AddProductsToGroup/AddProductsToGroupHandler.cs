namespace ProductCatalog.Features.Groups.AddProductsToGroup;

public record AddProductsToGroupCommand(Guid GroupId, List<Guid> ProductIds) : ICommand<GroupDetailResult>;

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

internal class AddProductsToGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<AddProductsToGroupCommand, GroupDetailResult>
{
    public async Task<GroupDetailResult> Handle(AddProductsToGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == command.GroupId, cancellationToken);

        if (group is null)
            throw new KeyNotFoundException("Group not found");

        var existingProductIds = await dbContext.GroupProducts
            .Where(gp => gp.GroupId == command.GroupId)
            .Select(gp => gp.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var productId in command.ProductIds.Except(existingProductIds))
        {
            dbContext.GroupProducts.Add(new GroupProduct
            {
                GroupId = command.GroupId,
                ProductId = productId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstAsync(g => g.Id == command.GroupId, cancellationToken);

        return new GroupDetailResult(
            group.Id,
            group.Name,
            group.Products.Select(p => new GroupProductInfo(p.Id, p.Name, p.UrlSlug)).ToList()
        );
    }
}
