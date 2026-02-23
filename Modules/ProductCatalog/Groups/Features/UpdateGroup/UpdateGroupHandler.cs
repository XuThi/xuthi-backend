namespace ProductCatalog.Groups.Features.UpdateGroup;

public record UpdateGroupCommand(Guid Id, UpdateGroupRequest Request) : ICommand<GroupResult>;

public record GroupResult(
    Guid Id,
    string Name,
    int ProductCount
);

internal class UpdateGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateGroupCommand, GroupResult>
{
    public async Task<GroupResult> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups
            .Include(g => g.Products)
            .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);

        if (group is null)
            throw new KeyNotFoundException("Group not found");

        if (command.Request.Name != null)
        {
            if (await dbContext.Groups.AnyAsync(g => g.Name == command.Request.Name && g.Id != command.Id, cancellationToken))
                throw new InvalidOperationException($"Group '{command.Request.Name}' already exists");

            group.Name = command.Request.Name;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GroupResult(group.Id, group.Name, group.Products.Count);
    }
}
