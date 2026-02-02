namespace Order.Features.GetOrders;

internal class GetOrdersHandler(OrderDbContext dbContext)
    : IQueryHandler<GetOrdersQuery, GetOrdersResult>
{
    public async Task<GetOrdersResult> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Orders
            .Include(o => o.Items)
            .AsQueryable();

        // Filter by email (for customer lookup)
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            query = query.Where(o => o.CustomerEmail.ToLower() == request.Email.ToLower());
        }

        // Filter by status
        if (request.Status.HasValue)
        {
            query = query.Where(o => o.Status == request.Status.Value);
        }

        // Filter by date range
        if (request.FromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummary(
                o.Id,
                o.OrderNumber,
                o.CustomerName,
                o.CustomerEmail,
                o.Total,
                o.Items.Count,
                o.Status.ToString(),
                o.PaymentStatus.ToString(),
                o.PaymentMethod.ToString(),
                o.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new GetOrdersResult(orders, totalCount, page, pageSize, totalPages);
    }
}
