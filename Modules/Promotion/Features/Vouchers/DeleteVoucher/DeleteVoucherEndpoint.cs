namespace Promotion.Features.Vouchers.DeleteVoucher;

// Endpoint
public class DeleteVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/vouchers/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteVoucherCommand(id);
            
            var result = await sender.Send(command);
            
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Vouchers")
        .WithSummary("Delete voucher")
        .RequireAuthorization("Admin");
    }
}
