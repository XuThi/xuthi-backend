using Core.DDD;

namespace Customer.Customers.Models;

/// <summary>
/// Loyalty points history for auditing.
/// </summary>
public class PointsHistory : Entity<Guid>
{
    public Guid CustomerId { get; set; }
    
    public PointsTransactionType Type { get; set; }
    public int Points { get; set; } // Positive for earned, negative for redeemed
    public int BalanceAfter { get; set; }
    
    public string Description { get; set; } = default!; // e.g., "Order #XT-20260131-001"
    public Guid? RelatedOrderId { get; set; }
    
    // Navigation
    public CustomerProfile Customer { get; set; } = null!;
}

public enum PointsTransactionType
{
    Earned = 1,     // Earned from purchase
    Redeemed = 2,   // Used for discount
    Expired = 3,    // Points expired
    Adjusted = 4,   // Manual adjustment by admin
    Bonus = 5       // Bonus points (promotions, birthday, etc.)
}
