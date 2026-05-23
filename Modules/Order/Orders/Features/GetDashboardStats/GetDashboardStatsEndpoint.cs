using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Order.Orders.Features.GetDashboardStats;

public class GetDashboardStatsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/dashboard", async (ISender sender) =>
        {
            var result = await sender.Send(new GetDashboardStatsQuery());
            return Results.Ok(result);
        })
        .RequireAuthorization("Staff")
        .Produces<DashboardStatsResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .WithName("GetAdminDashboardStats")
        .WithSummary("Get dashboard statistics and chart data for admins")
        .WithTags("Admin");
    }
}
