namespace ProductCatalog.VariantOptions.Dtos;

public class DimensionInfo
{
    public string DimensionId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string DisplayType { get; set; } = default!;
    public List<DimensionValueInfo> Values { get; set; } = [];
}
