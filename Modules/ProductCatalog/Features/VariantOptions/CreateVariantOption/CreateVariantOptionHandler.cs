using ProductCatalog.Features.VariantOptions.GetVariantOptions;

namespace ProductCatalog.Features.VariantOptions.CreateVariantOption;

public record CreateVariantOptionRequest(string Id, string Name, string DisplayType, List<string>? Values);
public record CreateVariantOptionCommand(CreateVariantOptionRequest Request) : ICommand<VariantOptionResult>;

public class CreateVariantOptionValidator : AbstractValidator<CreateVariantOptionCommand>
{
    public CreateVariantOptionValidator()
    {
        RuleFor(x => x.Request.Id).NotEmpty().Matches("^[a-z0-9-]+$").WithMessage("ID must be lowercase alphanumeric with hyphens");
        RuleFor(x => x.Request.Name).NotEmpty();
    }
}

internal class CreateVariantOptionHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateVariantOptionCommand, VariantOptionResult>
{
    public async Task<VariantOptionResult> Handle(CreateVariantOptionCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (await dbContext.VariantOptions.AnyAsync(o => o.Id == request.Id, cancellationToken))
        {
            throw new InvalidOperationException($"Variant option with ID '{request.Id}' already exists.");
        }

        var option = new VariantOption
        {
            Id = request.Id,
            Name = request.Name,
            DisplayType = request.DisplayType,
            Values = []
        };

        if (request.Values != null)
        {
            int sortOrder = 0;
            foreach (var val in request.Values)
            {
                option.Values.Add(new VariantOptionValue
                {
                    VariantOptionId = option.Id,
                    Value = val,
                    SortOrder = sortOrder++
                });
            }
        }

        dbContext.VariantOptions.Add(option);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new VariantOptionResult(option.Id, option.Name, option.DisplayType, option.Values.Select(v => v.Value).ToList());
    }
}
