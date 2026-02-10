namespace Promotion.Features.Vouchers.UpdateVoucher;

// Response
public record UpdateVoucherResponse(bool Success);

// Endpoint
public class UpdateVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/vouchers/{id:guid}", async (Guid id, UpdateVoucherRequest request, ISender sender) =>
        {
            var command = new UpdateVoucherCommand(id, request);
            
            var result = await sender.Send(command);
            
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Vouchers")
        .WithSummary("Update voucher")
        .RequireAuthorization("Admin");
    }
}
