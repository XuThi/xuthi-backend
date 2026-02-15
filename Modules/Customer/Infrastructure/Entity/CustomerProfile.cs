namespace Customer.Infrastructure.Entity;

/// <summary>
/// Customer profile linked to an external auth provider user.
/// Stores app-specific data like tier, points, addresses.
/// </summary>
public class CustomerProfile
{
    public Guid Id { get; set; }
    public string ExternalUserId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    
    // Tier/Loyalty
    public CustomerTier Tier { get; set; } = CustomerTier.Standard;
    public int LoyaltyPoints { get; set; } // Points earned from purchases
    public decimal TotalSpent { get; set; } // Lifetime spend
    public int TotalOrders { get; set; }
    
    // Tier benefits
    public decimal TierDiscountPercentage => Tier switch
    {
        CustomerTier.Standard => 0,
        CustomerTier.Silver => 3,   // 3% off
        CustomerTier.Gold => 5,     // 5% off  
        CustomerTier.Platinum => 10, // 10% off
        _ => 0
    };
    
    // Marketing preferences
    public bool AcceptsMarketing { get; set; } = true;
    public bool AcceptsSms { get; set; } = true;
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastOrderAt { get; set; }
    
    // Navigation
    public List<CustomerAddress> Addresses { get; set; } = [];
}

public enum CustomerTier
{
    Standard = 1,   // New customers
    Silver = 2,     // TotalSpent >= 1,000,000 VND
    Gold = 3,       // TotalSpent >= 5,000,000 VND
    Platinum = 4    // TotalSpent >= 10,000,000 VND
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}
