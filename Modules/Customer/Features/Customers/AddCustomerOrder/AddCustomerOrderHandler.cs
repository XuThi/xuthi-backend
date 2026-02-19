using Customer.Infrastructure.Data;
using Customer.Infrastructure.Entity;

namespace Customer.Features.Customers.AddCustomerOrder;

// Command and Result
public record AddCustomerOrderCommand(
    Guid CustomerId,
    decimal OrderTotal,
    int PointsEarned,
    Guid OrderId) : ICommand<AddCustomerOrderResult>;

public record AddCustomerOrderResult(CustomerTier NewTier, int TotalPoints);

// Validator
public class AddCustomerOrderCommandValidator : AbstractValidator<AddCustomerOrderCommand>
{
    public AddCustomerOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.OrderTotal).GreaterThan(0);
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

// Handler
internal class AddCustomerOrderHandler(CustomerDbContext db)
    : ICommandHandler<AddCustomerOrderCommand, AddCustomerOrderResult>
{
    // Tier thresholds in VND
    private const decimal SilverThreshold = 1_000_000;
    private const decimal GoldThreshold = 5_000_000;
    private const decimal PlatinumThreshold = 10_000_000;

    public async Task<AddCustomerOrderResult> Handle(AddCustomerOrderCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.Id == cmd.CustomerId, ct)
            ?? await db.Customers.FirstOrDefaultAsync(
                c => c.ExternalUserId == cmd.CustomerId.ToString(),
                ct);

        if (customer is null)
            return new AddCustomerOrderResult(CustomerTier.Standard, 0);

        // Update stats
        customer.TotalSpent += cmd.OrderTotal;
        customer.TotalOrders++;
        customer.LoyaltyPoints += cmd.PointsEarned;
        customer.LastOrderAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;

        // Calculate new tier
        var newTier = customer.TotalSpent switch
        {
            >= PlatinumThreshold => CustomerTier.Platinum,
            >= GoldThreshold => CustomerTier.Gold,
            >= SilverThreshold => CustomerTier.Silver,
            _ => CustomerTier.Standard
        };

        if (newTier > customer.Tier)
            customer.Tier = newTier;

        // Record points earned
        db.PointsHistory.Add(new PointsHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = PointsTransactionType.Earned,
            Points = cmd.PointsEarned,
            BalanceAfter = customer.LoyaltyPoints,
            Description = $"Order completed",
            RelatedOrderId = cmd.OrderId
        });

        await db.SaveChangesAsync(ct);

        return new AddCustomerOrderResult(customer.Tier, customer.LoyaltyPoints);
    }
}
