namespace ProductCatalog.VariantOptions.Models;

/// <summary>
/// Display types for variant options (how the frontend should render them)
/// </summary>
public enum VariantOptionDisplayType
{
    /// <summary>Dropdown selector (Size: 36, 37, 38)</summary>
    Dropdown = 1,
    
    /// <summary>Color swatches (shows actual color)</summary>
    ColorSwatch = 2,
    
    /// <summary>Image thumbnails (different patterns/designs)</summary>
    ImageSwatch = 3,
    
    /// <summary>Radio buttons or pills</summary>
    Button = 4
}
