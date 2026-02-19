using Customer.Infrastructure.Data;
using Customer.Infrastructure.Entity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Customer.Features.Customers.GetOrCreateCustomer;

// TODO: Seperate into GetCustomer and CreateCustomer if the logic becomes more complex. For now it's simple enough to combine since they share most of the code.

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
        var now = DateTime.UtcNow;
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.ExternalUserId == query.ExternalUserId, ct);

        var isNew = false;

        if (customer is null)
        {
            customer = new CustomerProfile
            {
                Id = Guid.NewGuid(),
                ExternalUserId = query.ExternalUserId,
                Email = query.Email,
                FullName = query.FullName,
                Tier = CustomerTier.Standard,
                LoyaltyPoints = 0,
                TotalSpent = 0,
                TotalOrders = 0,
                AcceptsMarketing = true,
                AcceptsSms = true,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now
            };

            db.Customers.Add(customer);

            try
            {
                await db.SaveChangesAsync(ct);
                isNew = true;
            }
            catch (DbUpdateException ex) when (IsExternalUserUniqueViolation(ex))
            {
                db.Entry(customer).State = EntityState.Detached;
                customer = await db.Customers.FirstAsync(c => c.ExternalUserId == query.ExternalUserId, ct);
                isNew = false;
            }
        }

        // Update last login
        var changed = false;
        customer.LastLoginAt = now;
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

        return new GetOrCreateCustomerResult(MapToDto(customer), isNew);
    }

    private static CustomerDto MapToDto(CustomerProfile c) => new(
        c.Id, c.ExternalUserId, c.Email, c.FullName, c.Phone,
        c.Tier, c.LoyaltyPoints, c.TotalSpent, c.TotalOrders,
        c.TierDiscountPercentage, c.CreatedAt, c.LastOrderAt);

    private static bool IsExternalUserUniqueViolation(DbUpdateException ex)
    {
        if (ex.GetBaseException() is not PostgresException pg)
        {
            return false;
        }

        return pg.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(pg.ConstraintName, "IX_Customers_ExternalUserId", StringComparison.OrdinalIgnoreCase);
    }
}
