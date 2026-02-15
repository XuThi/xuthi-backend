namespace Promotion.Features.Vouchers.ValidateVoucher;

// Request and Response
public record ValidateVoucherRequest(
    string Code,
    decimal CartTotal,
    List<Guid>? ProductIds = null,
    Guid? CategoryId = null,
    Guid? CustomerId = null,
    int? CustomerTier = null);

public record ValidateVoucherResponse(
    bool IsValid,
    string? ErrorMessage,
    Guid? VoucherId,
    decimal DiscountAmount);

// Endpoint
public class ValidateVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/vouchers/validate", async (ValidateVoucherRequest request, ISender sender) =>
        {
            var query = new ValidateVoucherQuery(
                request.Code,
                request.CartTotal,
                request.ProductIds,
                request.CategoryId,
                request.CustomerId,
                request.CustomerTier);
            
            var result = await sender.Send(query);
            
            var response = new ValidateVoucherResponse(
                result.IsValid,
                result.ErrorMessage,
                result.VoucherId,
                result.DiscountAmount);
            
            return Results.Ok(response);
        })
        .Produces<ValidateVoucherResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Vouchers")
        .WithSummary("Validate voucher for cart")
        .WithDescription("Returns discount amount if valid, or error message if not");
        // Public endpoint - no auth required for validation
    }
}
