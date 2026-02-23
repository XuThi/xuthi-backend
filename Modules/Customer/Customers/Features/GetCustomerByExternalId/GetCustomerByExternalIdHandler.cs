namespace Customer.Customers.Features.GetCustomerByExternalId;

// Query and Result
public record GetCustomerByExternalIdQuery(string ExternalUserId) : IQuery<GetCustomerByExternalIdResult>;
public record GetCustomerByExternalIdResult(CustomerDetailDto? Customer);

// Handler
internal class GetCustomerByExternalIdHandler(CustomerDbContext db)
    : IQueryHandler<GetCustomerByExternalIdQuery, GetCustomerByExternalIdResult>
{
    public async Task<GetCustomerByExternalIdResult> Handle(GetCustomerByExternalIdQuery query, CancellationToken ct)
    {
        var customer = await db.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.ExternalUserId == query.ExternalUserId, ct);

        return customer is null 
            ? new GetCustomerByExternalIdResult(null) 
            : new GetCustomerByExternalIdResult(MapToDetailDto(customer));
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
