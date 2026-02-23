namespace Promotion.Vouchers.Features.UpdateVoucher;

public record UpdateVoucherRouteRequest(Guid Id);
public record UpdateVoucherResponse(bool Success);

public class UpdateVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/vouchers/{id:guid}", async (
            [AsParameters] UpdateVoucherRouteRequest route,
            UpdateVoucherRequest request,
            ISender sender) =>
        {
            var command = new UpdateVoucherCommand(route.Id, request);
            var result = await sender.Send(command);
            var response = new UpdateVoucherResponse(result.Success);
            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<UpdateVoucherResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Vouchers")
        .WithSummary("Update voucher")
        .RequireAuthorization("Admin");
    }
}
