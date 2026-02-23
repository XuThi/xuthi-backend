namespace ProductCatalog.VariantOptions.Models;

public class VariantOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayType { get; set; } = "dropdown";
    public string? DefaultValue { get; set; }

    public List<VariantOptionValue> Values { get; set; } = [];
}
