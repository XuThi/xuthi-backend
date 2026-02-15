namespace ProductCatalog.Features.Groups.DeleteGroup;

public record DeleteGroupCommand(Guid Id) : ICommand<bool>;

internal class DeleteGroupHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteGroupCommand, bool>
{
    public async Task<bool> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await dbContext.Groups.FindAsync([command.Id], cancellationToken);
        if (group is null)
            return false;

        var groupProducts = await dbContext.GroupProducts
            .Where(gp => gp.GroupId == command.Id)
            .ToListAsync(cancellationToken);
        dbContext.GroupProducts.RemoveRange(groupProducts);

        dbContext.Groups.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
