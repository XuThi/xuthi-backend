using Contracts;
using Customer.Data;
using Microsoft.EntityFrameworkCore;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Customer.Customers.Features.GetCustomerByExternalId;

internal class CrossModuleGetCustomerByExternalIdHandler(CustomerDbContext db)
    : IRequestHandler<Contracts.GetCustomerByExternalIdQuery, Guid?>
{
    public async Task<Guid?> Handle(Contracts.GetCustomerByExternalIdQuery request, CancellationToken ct)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ExternalUserId == request.ExternalUserId, ct);

        return customer?.Id;
    }
}
