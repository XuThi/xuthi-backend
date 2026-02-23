using System.Text.Json.Serialization;
using Core.DDD;

namespace ProductCatalog.Groups.Models;

public class Group : Aggregate<Guid>
{
    public string Name { get; set; } = default!;
    [JsonIgnore]
    public List<Product> Products { get; set; } = [];
}
