using Customer.Infrastructure.Data;
using Customer.Infrastructure.Entity;
using Microsoft.EntityFrameworkCore;

namespace Customer.Features.Customers.GetOrCreateCustomer;

// TODO: Remove try catch here because why ? tf

// Query and Result
public record GetOrCreateCustomerQuery(string ExternalUserId, string Email, string? FullName = null)
    : IQuery<GetOrCreateCustomerResult>;
public record GetOrCreateCustomerResult(CustomerDto Customer, bool IsNew);

// Handler
internal class GetOrCreateCustomerHandler(CustomerDbContext db)
    : IQueryHandler<GetOrCreateCustomerQuery, GetOrCreateCustomerResult>
{
    public async Task<GetOrCreateCustomerResult> Handle(GetOrCreateCustomerQuery query, CancellationToken ct)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.ExternalUserId == query.ExternalUserId, ct);

        if (customer is null)
        {
            customer = new CustomerProfile
            {
                Id = Guid.NewGuid(),
                ExternalUserId = query.ExternalUserId,
                Email = query.Email,
                FullName = query.FullName,
                Tier = CustomerTier.Standard
            };
            db.Customers.Add(customer);
            try
            {
                await db.SaveChangesAsync(ct);
                return new GetOrCreateCustomerResult(MapToDto(customer), true);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();

                var existing = await db.Customers
                    .FirstOrDefaultAsync(c => c.ExternalUserId == query.ExternalUserId, ct);

                if (existing is null)
                {
                    throw;
                }

                return new GetOrCreateCustomerResult(MapToDto(existing), false);
            }
        }

        // Update last login
        var changed = false;
        customer.LastLoginAt = DateTime.UtcNow;
        changed = true;

        if (!string.IsNullOrWhiteSpace(query.Email) && !string.Equals(customer.Email, query.Email, StringComparison.OrdinalIgnoreCase))
        {
            customer.Email = query.Email;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(query.FullName) && !string.Equals(customer.FullName, query.FullName, StringComparison.Ordinal))
        {
            customer.FullName = query.FullName;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }

        return new GetOrCreateCustomerResult(MapToDto(customer), false);
    }

    private static CustomerDto MapToDto(CustomerProfile c) => new(
        c.Id, c.ExternalUserId, c.Email, c.FullName, c.Phone,
        c.Tier, c.LoyaltyPoints, c.TotalSpent, c.TotalOrders,
        c.TierDiscountPercentage, c.CreatedAt, c.LastOrderAt);
}
