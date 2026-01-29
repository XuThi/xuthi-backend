namespace ProductCatalog.Infrastructure.Dtos;

public class DimensionInfo
{
    public string DimensionId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string DisplayType { get; set; } = default!; // "dropdown", "color", "text", "image", "choice"
    public List<DimensionValueInfo> Values { get; set; } = [];
}