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
            .Include(o => o.Values)
            .FirstOrDefaultAsync(o => o.Id == command.Id, cancellationToken);

        if (option is null)
            throw new KeyNotFoundException($"Variant option '{command.Id}' not found.");

        var request = command.Request;
        if (!string.IsNullOrEmpty(request.Name)) option.Name = request.Name;
        if (!string.IsNullOrEmpty(request.DisplayType)) option.DisplayType = request.DisplayType;

        if (request.Values != null)
        {
            // Simple replace logic for now - clear and re-add
            // Ideally we should merge/update to preserve IDs if referenced, but Values are just strings + generated IDs
            // For now, let's keep it simple: clear old values, add new ones.
            // CAUTION: If VariantOptionSelection references VariantOptionValue by ID, this breaks referential integrity.
            // Checking VariantOptionSelection... it references VariantOptionId + Value (string).
            // VariantOptionSelection.cs: public string VariantOptionId { get; set; } public string Value { get; set; }
            // So creating new VariantOptionValue records is safe as long as the string Value matches.
            
            option.Values.Clear();
            int sortOrder = 0;
            foreach (var val in request.Values)
            {
                option.Values.Add(new VariantOptionValue
                {
                    Id = Guid.NewGuid(),
                    VariantOptionId = option.Id,
                    Value = val,
                    SortOrder = sortOrder++
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new VariantOptionResult(option.Id, option.Name, option.DisplayType, option.Values.OrderBy(v => v.SortOrder).Select(v => v.Value).ToList());
    }
}
