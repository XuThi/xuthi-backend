namespace Promotion.Features.Vouchers.GetVoucher;

public record GetVoucherRequest(Guid Id);

// Response
public record GetVoucherResponse(VoucherDto? Voucher);

// Endpoint
public class GetVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/vouchers/{id:guid}", async (
            [AsParameters] GetVoucherRequest request,
            ISender sender) =>
        {
            var query = new GetVoucherQuery(request.Id);
            
            var result = await sender.Send(query);

            var response = new GetVoucherResponse(result.Voucher);
            
            return result.Voucher is null 
                ? Results.NotFound() 
                : Results.Ok(response);
        })
        .Produces<GetVoucherResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Vouchers")
        .WithSummary("Get voucher by ID")
        .RequireAuthorization("Staff");
    }
}
