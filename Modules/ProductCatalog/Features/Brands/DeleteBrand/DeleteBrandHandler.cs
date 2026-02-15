using System;
using System.Collections.Generic;
using System.Text;

namespace ProductCatalog.Features.Brands.DeleteBrand;

public record DeleteBrandCommand(Guid Id) : ICommand<bool>;

internal class DeleteBrandHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<DeleteBrandCommand, bool>
{
    public async Task<bool> Handle(DeleteBrandCommand command, CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FindAsync([command.Id], cancellationToken);
        if (brand is null)
            throw new KeyNotFoundException("Brand not found");

        // Check if brand has products
        var hasProducts = await dbContext.Products.AnyAsync(p => p.BrandId == command.Id, cancellationToken);
        if (hasProducts)
            throw new InvalidOperationException("Cannot delete brand with products");

        dbContext.Brands.Remove(brand);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}