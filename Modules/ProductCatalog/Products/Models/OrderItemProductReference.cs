namespace ProductCatalog.Products.Models;

/// <summary>
/// Read-only projection of OrderItems table. Maps only the ProductId column so the
/// ProductCatalog module can run a cross-schema EXISTS check without taking a hard
/// dependency on the Order module's entity model or DbContext.
/// </summary>
public class OrderItemProductReference
{
    public Guid ProductId { get; set; }
}
