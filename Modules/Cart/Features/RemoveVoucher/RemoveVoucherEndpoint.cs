namespace Cart.Features.RemoveVoucher;

// Response
public record RemoveVoucherResponse(bool Success, CartDto? Cart);

// Endpoint
public class RemoveVoucherEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/cart/{cartId:guid}/voucher", async (Guid cartId, ISender sender) =>
        {
            var command = new RemoveVoucherCommand(cartId);
            
            var result = await sender.Send(command);

            var response = new RemoveVoucherResponse(result.Success, result.Cart);

            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<RemoveVoucherResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Shopping Cart")
        .WithSummary("Remove voucher from cart");
    }
}
