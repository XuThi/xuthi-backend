using Contracts;
using Order.Data;
using Order.Orders.Models;
using Microsoft.EntityFrameworkCore;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Order.Orders.Features.GetDeliveredProductIds;

internal class GetDeliveredProductIdsHandler(OrderDbContext db)
    : IRequestHandler<GetDeliveredProductIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDeliveredProductIdsQuery request, CancellationToken ct)
    {
        return await db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == request.CustomerId && o.Status == OrderStatus.Delivered)
            .SelectMany(o => o.Items.Select(i => i.ProductId))
            .Distinct()
            .ToListAsync(ct);
    }
}
