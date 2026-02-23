using Core.DDD;

namespace Customer.Customers.Models;

/// <summary>
/// Customer saved addresses for quick checkout.
/// </summary>
public class CustomerAddress : Entity<Guid>
{
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
    
    // Navigation
    public CustomerProfile Customer { get; set; } = null!;
}
