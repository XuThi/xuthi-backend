using Customer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Customer.Features.Customers.GetCustomers;

public record GetCustomersQuery(int Page = 1, int PageSize = 10, string? Search = null) : IQuery<GetCustomersResult>;

public record GetCustomersResult(IEnumerable<CustomerDto> Customers, long TotalCount);

public record CustomerDto(
    Guid Id, 
    string FullName, 
    string Email, 
    string? Phone, 
    string Tier, 
    decimal TotalSpent, 
    int TotalOrders, 
    DateTime CreatedAt);

internal class GetCustomersHandler(CustomerDbContext dbContext)
    : IQueryHandler<GetCustomersQuery, GetCustomersResult>
{
    public async Task<GetCustomersResult> Handle(GetCustomersQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            dbQuery = dbQuery.Where(c => 
                c.FullName.ToLower().Contains(search) || 
                c.Email.ToLower().Contains(search) || 
                (c.Phone != null && c.Phone.Contains(search)));
        }

        var totalCount = await dbQuery.LongCountAsync(cancellationToken);

        var customers = await dbQuery
            .OrderByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CustomerDto(
                c.Id,
                c.FullName,
                c.Email,
                c.Phone,
                c.Tier.ToString(),
                c.TotalSpent,
                c.TotalOrders,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return new GetCustomersResult(customers, totalCount);
    }
}
