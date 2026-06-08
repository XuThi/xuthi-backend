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
        var normalizedEmail = request.CustomerEmail?.Trim().ToLower();

        return await db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered)
            .Where(o => o.CustomerId == request.CustomerId
                || (!string.IsNullOrWhiteSpace(normalizedEmail)
                    && o.CustomerEmail.ToLower() == normalizedEmail))
            .AnyAsync(o => o.Items.Any(i => i.ProductId == request.ProductId), ct);
    }
}
