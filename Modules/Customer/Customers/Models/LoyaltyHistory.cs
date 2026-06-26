using Core.DDD;

namespace Customer.Customers.Models;

/// <summary>
/// Customer Loyalty audit history with after-state snapshots.
/// </summary>
public class LoyaltyHistory : Entity<Guid>
{
    public Guid CustomerId { get; set; }

    public LoyaltyTransactionType Type { get; set; }
    public int PointsDelta { get; set; }
    public int PointsBalanceAfter { get; set; }
    public decimal? LoyaltySpendDelta { get; set; }
    public decimal TotalLoyaltySpendAfter { get; set; }
    public int TotalOrdersAfter { get; set; }
    public CustomerTier TierAfter { get; set; }
    public DateTime OccurredAt { get; set; }

    public string Description { get; set; } = default!;
    public Guid? RelatedOrderId { get; set; }
    public string? OrderNumber { get; set; }

    public CustomerProfile Customer { get; set; } = null!;
}

public enum LoyaltyTransactionType
{
    Awarded = 1,
    Redeemed = 2,
    Expired = 3,
    Adjusted = 4,
    Bonus = 5,
    Reversed = 6
}
