using Customer.Infrastructure.Data;
using Customer.Infrastructure.Entity;

namespace Customer.Features.Customers.GetOrCreateCustomer;

// Query and Result
public record GetOrCreateCustomerQuery(string KeycloakUserId, string Email, string? FullName = null)
    : IQuery<GetOrCreateCustomerResult>;
public record GetOrCreateCustomerResult(CustomerDto Customer, bool IsNew);

// Handler
internal class GetOrCreateCustomerHandler(CustomerDbContext db)
    : IQueryHandler<GetOrCreateCustomerQuery, GetOrCreateCustomerResult>
{
    public async Task<GetOrCreateCustomerResult> Handle(GetOrCreateCustomerQuery query, CancellationToken ct)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.KeycloakUserId == query.KeycloakUserId, ct);

        if (customer is null)
        {
            customer = new CustomerProfile
            {
                Id = Guid.NewGuid(),
                KeycloakUserId = query.KeycloakUserId,
                Email = query.Email,
                FullName = query.FullName,
                Tier = CustomerTier.Standard
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);

            return new GetOrCreateCustomerResult(MapToDto(customer), true);
        }

        // Update last login
        customer.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new GetOrCreateCustomerResult(MapToDto(customer), false);
    }

    private static CustomerDto MapToDto(CustomerProfile c) => new(
        c.Id, c.KeycloakUserId, c.Email, c.FullName, c.Phone,
        c.Tier, c.LoyaltyPoints, c.TotalSpent, c.TotalOrders,
        c.TierDiscountPercentage, c.CreatedAt, c.LastOrderAt);
}
