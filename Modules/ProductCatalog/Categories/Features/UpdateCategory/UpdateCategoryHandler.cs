namespace ProductCatalog.Categories.Features.UpdateCategory;

public record UpdateCategoryCommand(Guid Id, UpdateCategoryRequest Request) : ICommand<CategoryResult>;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? ImageUrl,
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
            category.Description, category.ImageUrl, category.ParentCategoryId, category.SortOrder
        );
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.Replace("đ", "d").Replace("Đ", "d")
            .Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in slug)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        slug = sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-").Trim('-');
        return slug;
    }
}
