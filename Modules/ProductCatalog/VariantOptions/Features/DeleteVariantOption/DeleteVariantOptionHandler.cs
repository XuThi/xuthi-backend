namespace ProductCatalog.VariantOptions.Features.DeleteVariantOption;

public record DeleteVariantOptionCommand(string Id) : ICommand<bool>;

internal class DeleteVariantOptionHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteVariantOptionCommand, bool>
{
    public async Task<bool> Handle(DeleteVariantOptionCommand command, CancellationToken cancellationToken)
    {
        var option = await dbContext.VariantOptions.FindAsync([command.Id], cancellationToken);
        if (option is null)
            throw new KeyNotFoundException($"Variant option '{command.Id}' not found.");

        dbContext.VariantOptions.Remove(option);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
