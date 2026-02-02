namespace Order.Infrastructure.Entity;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    
    // Product snapshot (stored at time of order)
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public string ProductName { get; set; } = default!;
    public string VariantSku { get; set; } = default!;
    public string? VariantDescription { get; set; } // e.g., "Size 37, Black"
    public string? ImageUrl { get; set; }
    
    // Pricing at time of order
    public decimal UnitPrice { get; set; }
    public decimal? CompareAtPrice { get; set; } // Original price if discounted
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; } // UnitPrice * Quantity
    
    // Navigation
    public CustomerOrder Order { get; set; } = default!;
}
