namespace Customer.Customers.Features.GetCustomerLoyaltyHistory;

public record GetCustomerLoyaltyHistoryQuery(Guid CustomerId) : IQuery<GetCustomerLoyaltyHistoryResult>;

public record GetCustomerLoyaltyHistoryResult(IReadOnlyList<LoyaltyHistoryDto> History);

public record LoyaltyHistoryDto(
    Guid Id,
    Guid CustomerId,
    LoyaltyTransactionType Type,
    int PointsDelta,
    int PointsBalanceAfter,
    decimal? LoyaltySpendDelta,
    decimal TotalLoyaltySpendAfter,
    int TotalOrdersAfter,
    CustomerTier TierAfter,
    DateTime OccurredAt,
    DateTime? CreatedAt,
    string Description,
    Guid? RelatedOrderId,
    string? OrderNumber);

internal sealed class GetCustomerLoyaltyHistoryHandler(CustomerDbContext db)
    : IQueryHandler<GetCustomerLoyaltyHistoryQuery, GetCustomerLoyaltyHistoryResult>
{
    public async Task<GetCustomerLoyaltyHistoryResult> Handle(
        GetCustomerLoyaltyHistoryQuery query,
        CancellationToken ct)
    {
        var history = await db.LoyaltyHistory
            .AsNoTracking()
            .Where(h => h.CustomerId == query.CustomerId)
            .OrderByDescending(h => h.OccurredAt)
            .Select(h => new LoyaltyHistoryDto(
                h.Id,
                h.CustomerId,
                h.Type,
                h.PointsDelta,
                h.PointsBalanceAfter,
                h.LoyaltySpendDelta,
                h.TotalLoyaltySpendAfter,
                h.TotalOrdersAfter,
                h.TierAfter,
                h.OccurredAt,
                h.CreatedAt,
                h.Description,
                h.RelatedOrderId,
                h.OrderNumber))
            .ToListAsync(ct);

        return new GetCustomerLoyaltyHistoryResult(history);
    }
}
