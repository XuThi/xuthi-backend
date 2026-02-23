namespace Cart.ShoppingCarts.Features.ApplyVoucher;

// Request and Response
public record ApplyVoucherRequest(string VoucherCode);
public record ApplyVoucherResponse(bool Success, string? ErrorMessage, decimal DiscountAmount, CartDto? Cart);

// Endpoint
public class ApplyVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/cart/{cartId:guid}/voucher", async (
            Guid cartId,
            ApplyVoucherRequest request,
            ISender sender) =>
        {
            var command = new ApplyVoucherCommand(cartId, request.VoucherCode);

            var result = await sender.Send(command);

            var response = new ApplyVoucherResponse(
                result.Success,
                result.ErrorMessage,
                result.DiscountAmount,
                result.Cart);

            return result.Success
                ? Results.Ok(response)
                : Results.BadRequest(response);
        })
        .Produces<ApplyVoucherResponse>(StatusCodes.Status200OK)
        .Produces<ApplyVoucherResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Apply voucher to cart")
        .WithDescription("Validates and applies voucher discount");
    }
}
