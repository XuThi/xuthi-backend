namespace ProductCatalog.Infrastructure.Entity;

public class VariantOption
{
    public string Id { get; set; } = string.Empty; // e.g. "size", "color"
    public string Name { get; set; } = string.Empty; // e.g. "Size", "Color"
    public string DisplayType { get; set; } = "dropdown"; // dropdown, swatch, buttons
    public string? DefaultValue { get; set; }

    public List<VariantOptionValue> Values { get; set; } = [];
}
