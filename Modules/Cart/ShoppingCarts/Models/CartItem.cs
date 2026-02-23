using Core.DDD;

namespace Cart.ShoppingCarts.Models;

public class CartItem : Entity<Guid>
{
    public Guid CartId { get; set; }

    // Product reference
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }

    // Snapshot at add time (for display, actual price comes from ProductCatalog)
    public string ProductName { get; set; } = default!;
    public string VariantSku { get; set; } = default!;
    public string? VariantDescription { get; set; } // e.g., "Size 38, Black"
    public string? ImageUrl { get; set; }

    // Pricing (synced from ProductCatalog on add/update)
    public decimal UnitPrice { get; set; }
    public decimal? CompareAtPrice { get; set; } // Original price if on sale

    public int Quantity { get; set; }

    // Stock info (synced from ProductCatalog)
    public int AvailableStock { get; set; } // Current stock at time of check
    public bool IsInStock { get; set; }

    // Navigation
    public ShoppingCart Cart { get; set; } = null!;

    // Computed
    public decimal TotalPrice => UnitPrice * Quantity;
    public bool IsOnSale => CompareAtPrice.HasValue && CompareAtPrice > UnitPrice;
}
