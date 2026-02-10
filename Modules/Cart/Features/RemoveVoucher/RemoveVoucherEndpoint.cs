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
            
            return result.Success ? Results.Ok(result.Cart) : Results.NotFound();
        })
        .WithTags("Shopping Cart")
        .WithSummary("Remove voucher from cart");
    }
}
