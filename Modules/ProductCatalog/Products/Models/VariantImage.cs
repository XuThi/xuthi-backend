using System.Text.Json.Serialization;
using Core.DDD;

namespace ProductCatalog.Products.Models;

public class VariantImage : Entity<Guid>
{
    public Guid VariantId { get; set; }
    public Guid ImageId { get; set; }
    public string AltText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    [JsonIgnore]
    public Image Image { get; set; } = default!;
}
