namespace ProductCatalog.Features.Variants.DeleteVariant;

public record DeleteVariantCommand(Guid VariantId) : ICommand<bool>;

internal class DeleteVariantHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteVariantCommand, bool>
{
    public async Task<bool> Handle(DeleteVariantCommand command, CancellationToken cancellationToken)
    {
        var variant = await dbContext.Variants.FindAsync([command.VariantId], cancellationToken);
        if (variant is null)
            throw new KeyNotFoundException("Variant not found");

        variant.IsDeleted = true;
        variant.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
