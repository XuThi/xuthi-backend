using Microsoft.EntityFrameworkCore;
using Order.Data;
using Order.Orders.Models;
using ProductCatalog.Data;
using Customer.Data;

namespace Order.Orders.Features.GetDashboardStats;

public record GetDashboardStatsQuery : IRequest<DashboardStatsResult>;

public record DashboardStatsResult(
    decimal TotalRevenue,
    decimal RevenueChangePercentage,
    int OrderCount,
    decimal OrderCountChangePercentage,
    int ActiveProductsCount,
    int NewCustomersCount,
    decimal NewCustomersChangePercentage,
    List<RecentOrderDto> RecentOrders,
    List<MonthlyRevenueDto> MonthlyRevenue
);

public record RecentOrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    decimal Total,
    string Status,
    DateTime CreatedAt
);

public record MonthlyRevenueDto(
    string Month,
    decimal Revenue
);

internal class GetDashboardStatsHandler(
    OrderDbContext orderDb,
    ProductCatalogDbContext catalogDb,
    CustomerDbContext customerDb)
    : IRequestHandler<GetDashboardStatsQuery, DashboardStatsResult>
{
    public async Task<DashboardStatsResult> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = thisMonthStart.AddMonths(1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // 1. Calculate Revenue from completed, paid orders only.
        var thisMonthRevenue = await orderDb.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= thisMonthStart
                && o.CreatedAt < nextMonthStart
                && o.Status == OrderStatus.Delivered
                && o.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0;

        var lastMonthRevenue = await orderDb.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= lastMonthStart
                && o.CreatedAt < thisMonthStart
                && o.Status == OrderStatus.Delivered
                && o.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0;

        decimal revenueChange = 0;
        if (lastMonthRevenue > 0)
        {
            revenueChange = Math.Round(((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue) * 100, 2);
        }
        else if (thisMonthRevenue > 0)
        {
            revenueChange = 100;
        }

        // 2. Calculate Orders
        var thisMonthOrders = await orderDb.Orders
            .AsNoTracking()
            .CountAsync(o => o.CreatedAt >= thisMonthStart && o.CreatedAt < nextMonthStart, ct);

        var lastMonthOrders = await orderDb.Orders
            .AsNoTracking()
            .CountAsync(o => o.CreatedAt >= lastMonthStart && o.CreatedAt < thisMonthStart, ct);

        decimal ordersChange = 0;
        if (lastMonthOrders > 0)
        {
            ordersChange = Math.Round(((decimal)(thisMonthOrders - lastMonthOrders) / lastMonthOrders) * 100, 2);
        }
        else if (thisMonthOrders > 0)
        {
            ordersChange = 100;
        }

        // 3. Calculate Active Products
        var activeProductsCount = await catalogDb.Products
            .AsNoTracking()
            .CountAsync(p => p.IsActive && !p.IsDeleted, ct);

        // 4. Calculate New Customers
        var thisMonthCustomers = await customerDb.Customers
            .AsNoTracking()
            .CountAsync(c => c.CreatedAt >= thisMonthStart && c.CreatedAt < nextMonthStart, ct);

        var lastMonthCustomers = await customerDb.Customers
            .AsNoTracking()
            .CountAsync(c => c.CreatedAt >= lastMonthStart && c.CreatedAt < thisMonthStart, ct);

        decimal customersChange = 0;
        if (lastMonthCustomers > 0)
        {
            customersChange = Math.Round(((decimal)(thisMonthCustomers - lastMonthCustomers) / lastMonthCustomers) * 100, 2);
        }
        else if (thisMonthCustomers > 0)
        {
            customersChange = 100;
        }

        // 5. Recent Orders (Last 5 orders)
        var recentOrders = await orderDb.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new RecentOrderDto(
                o.Id,
                o.OrderNumber,
                o.CustomerName,
                o.Total,
                o.Status.ToString(),
                o.CreatedAt ?? DateTime.UtcNow
            ))
            .ToListAsync(ct);

        // 6. Monthly Revenue (Last 6 months)
        var monthlyRevenue = new List<MonthlyRevenueDto>();
        for (int i = 5; i >= 0; i--)
        {
            var mStart = thisMonthStart.AddMonths(-i);
            var mEnd = mStart.AddMonths(1);
            var monthName = mStart.ToString("MM/yyyy");

            var monthRevenue = await orderDb.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= mStart
                    && o.CreatedAt < mEnd
                    && o.Status == OrderStatus.Delivered
                    && o.PaymentStatus == PaymentStatus.Paid)
                .SumAsync(o => (decimal?)o.Total, ct) ?? 0;

            monthlyRevenue.Add(new MonthlyRevenueDto(monthName, monthRevenue));
        }

        return new DashboardStatsResult(
            thisMonthRevenue,
            revenueChange,
            thisMonthOrders,
            ordersChange,
            activeProductsCount,
            thisMonthCustomers,
            customersChange,
            recentOrders,
            monthlyRevenue
        );
    }
}
