namespace ProductCatalog.Categories.Features.CreateCategory;

public record CreateCategoryCommand(CreateCategoryRequest Request) : ICommand<CategoryResult>;

public record CategoryResult(
    Guid Id,
    string Name,
    string UrlSlug,
    string? Description,
    string? ImageUrl,
    Guid ParentCategoryId,
    int SortOrder
);

internal class CreateCategoryHandler(ProductCatalogDbContext dbContext)
    : ICommandHandler<CreateCategoryCommand, CategoryResult>
{
    public async Task<CategoryResult> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            UrlSlug = GenerateSlug(req.Name),
            Description = req.Description,
            ParentCategoryId = req.ParentCategoryId ?? Guid.Empty,
            SortOrder = req.SortOrder
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(category);
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

    private static CategoryResult MapToResult(Category c) =>
        new(c.Id, c.Name, c.UrlSlug, c.Description, c.ImageUrl, c.ParentCategoryId, c.SortOrder);
}
