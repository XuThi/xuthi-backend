namespace Promotion.Features.Vouchers.DeleteVoucher;

public record DeleteVoucherResponse(bool Success);

// Endpoint
public class DeleteVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/vouchers/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteVoucherCommand(id);
            
            var result = await sender.Send(command);

            var response = new DeleteVoucherResponse(result.Success);

            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<DeleteVoucherResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Vouchers")
        .WithSummary("Delete voucher")
        .RequireAuthorization("Admin");
    }
}
