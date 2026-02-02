namespace Customer.Infrastructure.Entity;

/// <summary>
/// Customer saved addresses for quick checkout.
/// </summary>
public class CustomerAddress
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    
    // Address info
    public string Label { get; set; } = default!; // "Home", "Office", etc.
    public string RecipientName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!; // Street address
    public string Ward { get; set; } = default!;
    public string District { get; set; } = default!;
    public string City { get; set; } = default!;
    public string? Note { get; set; } // Delivery instructions
    
    public bool IsDefault { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public CustomerProfile Customer { get; set; } = null!;
}
