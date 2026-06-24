namespace Customer.Customers.Features.GetCustomerPromotionFacts;

public record GetCustomerPromotionFactsQuery(Guid CustomerId) : IQuery<GetCustomerPromotionFactsResult>;

public record GetCustomerPromotionFactsResult(CustomerPromotionFacts? Customer);

public record CustomerPromotionFacts(Guid CustomerId, CustomerTier Tier, int TotalOrders);

internal class GetCustomerPromotionFactsHandler(CustomerDbContext db)
    : IQueryHandler<GetCustomerPromotionFactsQuery, GetCustomerPromotionFactsResult>
{
    public async Task<GetCustomerPromotionFactsResult> Handle(GetCustomerPromotionFactsQuery query, CancellationToken ct)
    {
        var facts = await db.Customers
            .Where(c => c.Id == query.CustomerId)
            .Select(c => new CustomerPromotionFacts(c.Id, c.Tier, c.TotalOrders))
            .FirstOrDefaultAsync(ct);

        return new GetCustomerPromotionFactsResult(facts);
    }
}
