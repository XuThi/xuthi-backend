using Microsoft.EntityFrameworkCore;
using ProductCatalog.Features.VariantOptions.GetVariantOptions;

namespace ProductCatalog.Features.VariantOptions.UpdateVariantOption;

public record UpdateVariantOptionRequest(string? Name, string? DisplayType, List<string>? Values);
public record UpdateVariantOptionCommand(string Id, UpdateVariantOptionRequest Request) : ICommand<VariantOptionResult>;

internal class UpdateVariantOptionHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateVariantOptionCommand, VariantOptionResult>
{
    public async Task<VariantOptionResult> Handle(UpdateVariantOptionCommand command, CancellationToken cancellationToken)
    {
        var option = await dbContext.VariantOptions
            .FirstOrDefaultAsync(o => o.Id == command.Id, cancellationToken);

        if (option is null)
            throw new KeyNotFoundException($"Variant option '{command.Id}' not found.");

        var request = command.Request;
        if (!string.IsNullOrEmpty(request.Name)) option.Name = request.Name;
        if (!string.IsNullOrEmpty(request.DisplayType)) option.DisplayType = request.DisplayType;

        if (request.Values != null)
        {
            var normalized = request.Values
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingValues = await dbContext.VariantOptionValues
                .Where(v => v.VariantOptionId == option.Id)
                .ToListAsync(cancellationToken);

            if (existingValues.Count > 0)
                dbContext.VariantOptionValues.RemoveRange(existingValues);

            for (var i = 0; i < normalized.Count; i++)
            {
                var value = normalized[i];
                dbContext.VariantOptionValues.Add(new VariantOptionValue
                {
                    Id = Guid.NewGuid(),
                    VariantOptionId = option.Id,
                    Value = value,
                    DisplayValue = value,
                    SortOrder = i
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var values = await dbContext.VariantOptionValues
            .Where(v => v.VariantOptionId == option.Id)
            .OrderBy(v => v.SortOrder)
            .Select(v => v.Value)
            .ToListAsync(cancellationToken);

        return new VariantOptionResult(option.Id, option.Name, option.DisplayType, values);
    }
}
