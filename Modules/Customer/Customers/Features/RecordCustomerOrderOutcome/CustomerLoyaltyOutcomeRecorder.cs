using Contracts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Customer.Customers.Features.RecordCustomerOrderOutcome;

internal sealed class CustomerOrderOutcomeOccurredHandler(CustomerLoyaltyOutcomeRecorder recorder)
    : INotificationHandler<CustomerOrderOutcomeOccurred>
{
    public Task Handle(CustomerOrderOutcomeOccurred notification, CancellationToken cancellationToken)
    {
        return recorder.RecordAsync(notification, cancellationToken);
    }
}

public sealed class CustomerLoyaltyOutcomeRecorder(
    CustomerDbContext db,
    TimeProvider timeProvider,
    ILogger<CustomerLoyaltyOutcomeRecorder> logger)
{
    public async Task RecordAsync(CustomerOrderOutcomeOccurred outcome, CancellationToken ct)
    {
        ValidateMoneyFacts(outcome);

        switch (outcome.Outcome)
        {
            case CustomerOrderOutcome.Delivered:
                await RecordAwardAsync(outcome, ct);
                break;
            case CustomerOrderOutcome.Returned:
            case CustomerOrderOutcome.Cancelled:
                await RecordReversalAsync(outcome, ct);
                break;
            default:
                throw new InvalidOperationException($"Unsupported Customer Order Outcome: {outcome.Outcome}");
        }
    }

    private async Task RecordAwardAsync(CustomerOrderOutcomeOccurred outcome, CancellationToken ct)
    {
        var awardCalculation = CustomerLoyaltyPolicy.CalculateAward(
            outcome.Subtotal,
            outcome.DiscountAmount);
        var loyaltySpend = awardCalculation.LoyaltySpend;
        if (loyaltySpend <= 0)
            throw new InvalidOperationException("Loyalty Spend must be positive for a Loyalty Award.");

        var points = awardCalculation.Points;
        var existingAward = await db.LoyaltyHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                h => h.RelatedOrderId == outcome.OrderId
                    && h.Type == LoyaltyTransactionType.Awarded,
                ct);

        if (existingAward is not null)
        {
            EnsureMatchingAward(existingAward, outcome, loyaltySpend, points);
            return;
        }

        var customer = await LoadCustomerAsync(outcome.CustomerId, ct);
        customer.ApplyLoyaltyAward(loyaltySpend, points, outcome.OccurredAt);
        customer.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        db.LoyaltyHistory.Add(new LoyaltyHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = LoyaltyTransactionType.Awarded,
            PointsDelta = points,
            PointsBalanceAfter = customer.LoyaltyPoints,
            LoyaltySpendDelta = loyaltySpend,
            TotalLoyaltySpendAfter = customer.TotalLoyaltySpend,
            TotalOrdersAfter = customer.TotalOrders,
            TierAfter = customer.Tier,
            OccurredAt = outcome.OccurredAt,
            Description = $"Order {outcome.OrderNumber} awarded Customer Loyalty.",
            RelatedOrderId = outcome.OrderId,
            OrderNumber = outcome.OrderNumber,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            DetachAddedLoyaltyHistory(outcome.OrderId, LoyaltyTransactionType.Awarded);
            await ReloadTrackedCustomerAsync(outcome.CustomerId, ct);

            var racedAward = await db.LoyaltyHistory
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    h => h.RelatedOrderId == outcome.OrderId
                        && h.Type == LoyaltyTransactionType.Awarded,
                    ct);

            if (racedAward is not null)
            {
                EnsureMatchingAward(racedAward, outcome, loyaltySpend, points);
                return;
            }

            throw;
        }
    }

    private async Task RecordReversalAsync(CustomerOrderOutcomeOccurred outcome, CancellationToken ct)
    {
        var customer = await LoadCustomerAsync(outcome.CustomerId, ct);
        var award = await db.LoyaltyHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                h => h.RelatedOrderId == outcome.OrderId
                    && h.Type == LoyaltyTransactionType.Awarded,
                ct);

        if (award is null)
        {
            logger.LogInformation(
                "Customer Order Outcome {Outcome} for order {OrderId} has no prior Loyalty Award; no Customer Loyalty reversal was recorded.",
                outcome.Outcome,
                outcome.OrderId);
            return;
        }

        if (award.LoyaltySpendDelta is null)
            throw new InvalidOperationException("Cannot reverse a legacy Loyalty Award with unknown Loyalty Spend.");

        EnsureReversalOutcomeMatchesAward(award, outcome);

        var existingReversal = await db.LoyaltyHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                h => h.RelatedOrderId == outcome.OrderId
                    && h.Type == LoyaltyTransactionType.Reversed,
                ct);

        if (existingReversal is not null)
        {
            EnsureMatchingReversal(existingReversal, award, outcome);
            return;
        }

        var latestRemainingAwardOccurredAt = await GetLatestRemainingAwardOccurredAtAsync(customer.Id, outcome.OrderId, ct);
        customer.ApplyLoyaltyReversal(
            award.LoyaltySpendDelta.Value,
            award.PointsDelta,
            latestRemainingAwardOccurredAt);
        customer.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;

        db.LoyaltyHistory.Add(new LoyaltyHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = LoyaltyTransactionType.Reversed,
            PointsDelta = -award.PointsDelta,
            PointsBalanceAfter = customer.LoyaltyPoints,
            LoyaltySpendDelta = -award.LoyaltySpendDelta.Value,
            TotalLoyaltySpendAfter = customer.TotalLoyaltySpend,
            TotalOrdersAfter = customer.TotalOrders,
            TierAfter = customer.Tier,
            OccurredAt = outcome.OccurredAt,
            Description = $"Order {outcome.OrderNumber} reversed Customer Loyalty.",
            RelatedOrderId = outcome.OrderId,
            OrderNumber = outcome.OrderNumber,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            DetachAddedLoyaltyHistory(outcome.OrderId, LoyaltyTransactionType.Reversed);
            await ReloadTrackedCustomerAsync(outcome.CustomerId, ct);

            var racedReversal = await db.LoyaltyHistory
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    h => h.RelatedOrderId == outcome.OrderId
                        && h.Type == LoyaltyTransactionType.Reversed,
                    ct);

            if (racedReversal is not null)
            {
                EnsureMatchingReversal(racedReversal, award, outcome);
                return;
            }

            throw;
        }
    }

    private async Task<CustomerProfile> LoadCustomerAsync(Guid customerId, CancellationToken ct)
    {
        return await db.Customers.SingleOrDefaultAsync(c => c.Id == customerId, ct)
            ?? throw new InvalidOperationException($"Customer '{customerId}' was not found for Customer Loyalty.");
    }

    private async Task ReloadTrackedCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var customerEntry = db.ChangeTracker
            .Entries<CustomerProfile>()
            .FirstOrDefault(e => e.Entity.Id == customerId);

        if (customerEntry is not null && customerEntry.State != EntityState.Detached)
            await customerEntry.ReloadAsync(ct);
    }

    private void DetachAddedLoyaltyHistory(Guid orderId, LoyaltyTransactionType type)
    {
        foreach (var entry in db.ChangeTracker
            .Entries<LoyaltyHistory>()
            .Where(e => e.State == EntityState.Added
                && e.Entity.RelatedOrderId == orderId
                && e.Entity.Type == type)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task<DateTime?> GetLatestRemainingAwardOccurredAtAsync(
        Guid customerId,
        Guid reversingOrderId,
        CancellationToken ct)
    {
        var reversedOrderIds = await db.LoyaltyHistory
            .AsNoTracking()
            .Where(h => h.CustomerId == customerId
                && h.Type == LoyaltyTransactionType.Reversed
                && h.RelatedOrderId.HasValue)
            .Select(h => h.RelatedOrderId!.Value)
            .ToListAsync(ct);

        return await db.LoyaltyHistory
            .AsNoTracking()
            .Where(h => h.CustomerId == customerId
                && h.Type == LoyaltyTransactionType.Awarded
                && h.RelatedOrderId != reversingOrderId
                && h.RelatedOrderId.HasValue
                && !reversedOrderIds.Contains(h.RelatedOrderId.Value))
            .OrderByDescending(h => h.OccurredAt)
            .Select(h => (DateTime?)h.OccurredAt)
            .FirstOrDefaultAsync(ct);
    }

    private static void ValidateMoneyFacts(CustomerOrderOutcomeOccurred outcome)
    {
        if (outcome.Subtotal < 0 || outcome.DiscountAmount < 0 || outcome.ShippingFee < 0 || outcome.Total < 0)
            throw new InvalidOperationException("Customer Order Outcome money facts cannot be negative.");

        if (outcome.DiscountAmount > outcome.Subtotal)
            throw new InvalidOperationException("Customer Order Outcome discount cannot exceed subtotal.");

        var expectedTotal = outcome.Subtotal - outcome.DiscountAmount + outcome.ShippingFee;
        if (outcome.Total != expectedTotal)
            throw new InvalidOperationException("Customer Order Outcome total does not match subtotal, discount, and shipping.");
    }

    private static void EnsureMatchingAward(
        LoyaltyHistory existingAward,
        CustomerOrderOutcomeOccurred outcome,
        decimal loyaltySpend,
        int points)
    {
        if (existingAward.CustomerId != outcome.CustomerId
            || existingAward.OrderNumber != outcome.OrderNumber
            || existingAward.LoyaltySpendDelta != loyaltySpend
            || existingAward.PointsDelta != points)
        {
            throw new InvalidOperationException("Conflicting duplicate Customer Loyalty Award outcome.");
        }
    }

    private static void EnsureMatchingReversal(
        LoyaltyHistory existingReversal,
        LoyaltyHistory award,
        CustomerOrderOutcomeOccurred outcome)
    {
        EnsureReversalOutcomeMatchesAward(award, outcome);

        if (existingReversal.CustomerId != outcome.CustomerId
            || existingReversal.OrderNumber != outcome.OrderNumber
            || existingReversal.PointsDelta != -award.PointsDelta
            || existingReversal.LoyaltySpendDelta != -award.LoyaltySpendDelta)
        {
            throw new InvalidOperationException("Conflicting duplicate Customer Loyalty Reversal outcome.");
        }
    }

    private static void EnsureReversalOutcomeMatchesAward(
        LoyaltyHistory award,
        CustomerOrderOutcomeOccurred outcome)
    {
        var reversalCalculation = CustomerLoyaltyPolicy.CalculateAward(
            outcome.Subtotal,
            outcome.DiscountAmount);

        if (award.CustomerId != outcome.CustomerId
            || award.OrderNumber != outcome.OrderNumber
            || award.LoyaltySpendDelta != reversalCalculation.LoyaltySpend
            || award.PointsDelta != reversalCalculation.Points)
        {
            throw new InvalidOperationException("Conflicting duplicate Customer Loyalty Reversal outcome.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }
}
