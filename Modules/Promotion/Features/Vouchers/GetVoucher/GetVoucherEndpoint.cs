namespace Promotion.Features.Vouchers.GetVoucher;

// Response
public record GetVoucherResponse(VoucherDto? Voucher);

// Endpoint
public class GetVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vouchers/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetVoucherQuery(id);
            
            var result = await sender.Send(query);
            
            return result.Voucher is null 
                ? Results.NotFound() 
                : Results.Ok(result.Voucher);
        })
        .WithTags("Vouchers")
        .WithSummary("Get voucher by ID")
        .RequireAuthorization("Staff");
    }
}
