using Contracts;
using Order.Data;
using Order.Orders.Models;
using Microsoft.EntityFrameworkCore;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Order.Orders.Features.VerifyBuyer;

internal class VerifyBuyerHandler(OrderDbContext db)
    : IRequestHandler<VerifyBuyerQuery, bool>
{
    public async Task<bool> Handle(VerifyBuyerQuery request, CancellationToken ct)
    {
        return await db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == request.CustomerId && o.Status == OrderStatus.Delivered)
            .AnyAsync(o => o.Items.Any(i => i.ProductId == request.ProductId), ct);
    }
}
