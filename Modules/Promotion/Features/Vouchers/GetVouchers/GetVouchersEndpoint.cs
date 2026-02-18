namespace Promotion.Features.Vouchers.GetVouchers;

// TODO: Why tf is IsActive = null the hell ?
public record GetVouchersRequest(bool? IsActive = null, bool? ValidOnly = null);

// Response
public record GetVouchersResponse(List<VoucherDto> Vouchers);

// Endpoint
public class GetVouchersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vouchers", async (
            [AsParameters] GetVouchersRequest request,
            ISender sender) =>
        {
            var query = new GetVouchersQuery(request.IsActive, request.ValidOnly);
            
            var result = await sender.Send(query);

            var response = new GetVouchersResponse(result.Vouchers);

            return Results.Ok(response);
        })
        .Produces<GetVouchersResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Vouchers")
        .WithSummary("Get all vouchers")
        .WithDescription("Filter by isActive and validOnly")
        .RequireAuthorization("Staff");
    }
}
