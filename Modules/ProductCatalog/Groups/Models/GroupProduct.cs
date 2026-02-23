using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Groups.Models;

[PrimaryKey(nameof(ProductId), nameof(GroupId))]
public class GroupProduct
{
    public Guid ProductId { get; set; }
    [JsonIgnore]
    public Product Product { get; set; } = default!;
    public Guid GroupId { get; set; }
    [JsonIgnore]
    public Group Group { get; set; } = default!;
}
