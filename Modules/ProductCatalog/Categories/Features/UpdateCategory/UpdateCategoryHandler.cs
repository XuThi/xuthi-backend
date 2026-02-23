namespace ProductCatalog.Categories.Features.UpdateCategory;

public record UpdateCategoryCommand(Guid Id, UpdateCategoryRequest Request) : ICommand<CategoryResult>;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    Guid? ParentCategoryId,
    int SortOrder
);

internal class UpdateCategoryHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<UpdateCategoryCommand, CategoryResult>
{
    public async Task<CategoryResult> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FindAsync([command.Id], cancellationToken);
        if (category is null)
            throw new KeyNotFoundException("Category not found");

        var req = command.Request;

        if (req.Name != null)
        {
            category.Name = req.Name;
            category.UrlSlug = GenerateSlug(req.Name);
        }
        if (!string.IsNullOrWhiteSpace(req.UrlSlug))
            category.UrlSlug = GenerateSlug(req.UrlSlug);
        if (req.Description != null) category.Description = req.Description;
        if (req.ParentCategoryId != null) category.ParentCategoryId = (Guid)req.ParentCategoryId;
        if (req.SortOrder.HasValue) category.SortOrder = req.SortOrder.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryResult(
            category.Id, category.Name, category.UrlSlug,
            category.Description, category.ParentCategoryId, category.SortOrder
        );
    }

    private static string GenerateSlug(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lowered.Length);
        var previousDash = false;

        foreach (var ch in lowered.Normalize(System.Text.NormalizationForm.FormD))
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;

            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                sb.Append(ch);
                previousDash = false;
            }
            else if (!previousDash)
            {
                sb.Append('-');
                previousDash = true;
            }
        }

        return sb.ToString().Trim('-');
    }
}
