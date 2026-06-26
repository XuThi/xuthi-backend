using Core.DDD;

namespace Customer.Customers.Models;

/// <summary>
/// Customer profile linked to an external auth provider user.
/// Stores app-specific data like tier, points, addresses.
/// </summary>
public class CustomerProfile : Aggregate<Guid>
{
    public string ExternalUserId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    
    // Customer Loyalty
    public CustomerTier Tier { get; set; } = CustomerTier.Standard;
    public int LoyaltyPoints { get; set; } // Points earned from purchases
    public decimal TotalLoyaltySpend { get; set; } // Loyalty-counting lifetime spend
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
    public bool AcceptsMarketing { get; set; }
    public bool AcceptsSms { get; set; }
    
    // Timestamps
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastOrderAt { get; set; }
    
    // Navigation
    public List<CustomerAddress> Addresses { get; set; } = [];

    public void ApplyLoyaltyAward(decimal loyaltySpend, int points, DateTime occurredAt)
    {
        if (loyaltySpend <= 0)
            throw new InvalidOperationException("Loyalty Spend must be positive for a Loyalty Award.");

        if (points < 0)
            throw new InvalidOperationException("Loyalty Points award cannot be negative.");

        TotalLoyaltySpend += loyaltySpend;
        LoyaltyPoints += points;
        TotalOrders++;
        LastOrderAt = occurredAt;
        RecomputeTier();
    }

    public void ApplyLoyaltyReversal(
        decimal loyaltySpend,
        int points,
        DateTime? latestRemainingAwardOccurredAt)
    {
        if (loyaltySpend <= 0)
            throw new InvalidOperationException("Loyalty Spend reversal must be positive.");

        if (points < 0)
            throw new InvalidOperationException("Loyalty Points reversal cannot be negative.");

        if (TotalLoyaltySpend - loyaltySpend < 0)
            throw new InvalidOperationException("Customer Loyalty Spend cannot become negative.");

        if (LoyaltyPoints - points < 0)
            throw new InvalidOperationException("Loyalty Points cannot become negative.");

        if (TotalOrders - 1 < 0)
            throw new InvalidOperationException("Customer Loyalty order count cannot become negative.");

        TotalLoyaltySpend -= loyaltySpend;
        LoyaltyPoints -= points;
        TotalOrders--;
        LastOrderAt = latestRemainingAwardOccurredAt;
        RecomputeTier();
    }

    private void RecomputeTier()
    {
        Tier = CustomerLoyaltyPolicy.CalculateTier(TotalLoyaltySpend);
    }
}

public enum CustomerTier
{
    Standard = 1,   // New customers
    Silver = 2,     // TotalLoyaltySpend >= 1,000,000 VND
    Gold = 3,       // TotalLoyaltySpend >= 5,000,000 VND
    Platinum = 4    // TotalLoyaltySpend >= 10,000,000 VND
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}
