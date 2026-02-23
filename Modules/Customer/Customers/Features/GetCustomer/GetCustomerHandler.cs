namespace Customer.Customers.Features.GetCustomer;

// Query and Result
public record GetCustomerQuery(Guid Id) : IQuery<GetCustomerResult>;
public record GetCustomerResult(CustomerDetailDto? Customer);

// Handler
internal class GetCustomerHandler(CustomerDbContext db)
    : IQueryHandler<GetCustomerQuery, GetCustomerResult>
{
    public async Task<GetCustomerResult> Handle(GetCustomerQuery query, CancellationToken ct)
    {
        var customer = await db.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        return customer is null 
            ? new GetCustomerResult(null) 
            : new GetCustomerResult(MapToDetailDto(customer));
    }

    private static CustomerDetailDto MapToDetailDto(CustomerProfile c) => new(
        c.Id, c.ExternalUserId, c.Email, c.FullName, c.Phone,
        c.DateOfBirth, c.Gender, c.Tier, c.LoyaltyPoints, c.TotalSpent, c.TotalOrders,
        c.TierDiscountPercentage, c.AcceptsMarketing, c.AcceptsSms,
        c.CreatedAt, c.LastLoginAt, c.LastOrderAt,
        c.Addresses.Select(a => new CustomerAddressDto(
            a.Id, a.Label, a.RecipientName, a.Phone, a.Address,
            a.Ward, a.District, a.City, a.Note, a.IsDefault
        )).ToList());
}
