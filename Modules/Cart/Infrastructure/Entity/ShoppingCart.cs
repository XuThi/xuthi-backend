namespace Cart.Infrastructure.Entity;

/// <summary>
/// Shopping cart. Can be anonymous (SessionId) or linked to a customer.
/// Cart auto-expires after a period of inactivity.
/// </summary>
public class ShoppingCart
{
    public Guid Id { get; set; }
    
    // Identity - either SessionId for anonymous or CustomerId for logged in
    public string? SessionId { get; set; } // Browser session ID (anonymous)
    public Guid? CustomerId { get; set; } // Customer ID (logged in)
    
    // Applied voucher (validated, not yet used until checkout)
    public Guid? AppliedVoucherId { get; set; }
    public string? AppliedVoucherCode { get; set; }
    public decimal VoucherDiscount { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } // Auto-expire inactive carts
    
    // Items
    public List<CartItem> Items { get; set; } = [];
    
    // Computed totals (calculated on read, not stored)
    public decimal Subtotal => Items.Sum(i => i.TotalPrice);
    public decimal Total => Math.Max(0, Subtotal - VoucherDiscount);
    public int TotalItems => Items.Sum(i => i.Quantity);
}

public class CartItem
{
    public Guid Id { get; set; }
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
    
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ShoppingCart Cart { get; set; } = null!;
    
    // Computed
    public decimal TotalPrice => UnitPrice * Quantity;
    public bool IsOnSale => CompareAtPrice.HasValue && CompareAtPrice > UnitPrice;
}
