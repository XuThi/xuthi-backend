namespace Promotion.Features.Vouchers.GetVouchers;

// Response
public record GetVouchersResponse(List<VoucherDto> Vouchers);

// Endpoint
public class GetVouchersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vouchers", async (
            [AsParameters] bool isActive,
            [AsParameters] bool validOnly,
            ISender sender) =>
        {
            var query = new GetVouchersQuery(isActive, validOnly);
            
            var result = await sender.Send(query);
            
            return Results.Ok(result.Vouchers);
        })
        .WithTags("Vouchers")
        .WithSummary("Get all vouchers")
        .WithDescription("Filter by isActive and validOnly")
        .RequireAuthorization("Staff");
    }
}
