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

namespace Order.Orders.Features.GetFrequentlyBoughtTogether;

internal class GetFrequentlyBoughtTogetherHandler(OrderDbContext db)
    : IRequestHandler<GetFrequentlyBoughtTogetherProductIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetFrequentlyBoughtTogetherProductIdsQuery request, CancellationToken ct)
    {
        var orderIdsWithCurrentProduct = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered && o.Items.Any(i => i.ProductId == request.ProductId))
            .Select(o => o.Id)
            .ToListAsync(ct);

        if (orderIdsWithCurrentProduct.Count == 0)
            return [];

        return await db.Orders
            .AsNoTracking()
            .Where(o => orderIdsWithCurrentProduct.Contains(o.Id))
            .SelectMany(o => o.Items)
            .Where(i => i.ProductId != request.ProductId)
            .GroupBy(i => i.ProductId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(request.Limit)
            .ToListAsync(ct);
    }
}
