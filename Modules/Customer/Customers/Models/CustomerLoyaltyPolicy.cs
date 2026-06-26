namespace Customer.Customers.Models;

public readonly record struct LoyaltyAwardCalculation(decimal LoyaltySpend, int Points);

public static class CustomerLoyaltyPolicy
{
    public static LoyaltyAwardCalculation CalculateAward(
        decimal subtotal,
        decimal discountAmount)
    {
        var loyaltySpend = subtotal - discountAmount;
        var points = (int)Math.Floor(loyaltySpend / 10_000m);

        return new LoyaltyAwardCalculation(loyaltySpend, points);
    }

    public static CustomerTier CalculateTier(decimal totalLoyaltySpend)
    {
        return totalLoyaltySpend switch
        {
            >= 10_000_000m => CustomerTier.Platinum,
            >= 5_000_000m => CustomerTier.Gold,
            >= 1_000_000m => CustomerTier.Silver,
            _ => CustomerTier.Standard
        };
    }
}
