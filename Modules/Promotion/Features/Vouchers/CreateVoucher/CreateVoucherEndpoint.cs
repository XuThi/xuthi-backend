namespace Promotion.Features.Vouchers.CreateVoucher;

// Response
public record CreateVoucherResponse(Guid Id);

// Endpoint
public class CreateVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/vouchers", async (CreateVoucherRequest request, ISender sender) =>
        {
            var command = new CreateVoucherCommand(request);
            
            var result = await sender.Send(command);
            
            return Results.Created($"/api/vouchers/{result.Id}", new CreateVoucherResponse(result.Id));
        })
        .Produces<CreateVoucherResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Vouchers")
        .WithSummary("Create new voucher")
        .RequireAuthorization("Admin");
    }
}
