namespace ProductCatalog.Features.Groups.CreateGroup;

public record CreateGroupCommand(CreateGroupRequest Request) : ICommand<GroupResult>;

public record GroupResult(
    Guid Id,
    string Name,
    int ProductCount
);

internal class CreateGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateGroupCommand, GroupResult>
{
    public async Task<GroupResult> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        if (await dbContext.Groups.AnyAsync(g => g.Name == req.Name, cancellationToken))
            throw new InvalidOperationException($"Group '{req.Name}' already exists");

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = req.Name
        };

        dbContext.Groups.Add(group);

        if (req.ProductIds?.Count > 0)
        {
            foreach (var productId in req.ProductIds)
            {
                dbContext.GroupProducts.Add(new GroupProduct
                {
                    GroupId = group.Id,
                    ProductId = productId
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GroupResult(group.Id, group.Name, req.ProductIds?.Count ?? 0);
    }
}
