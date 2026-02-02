namespace Order.Features.UpdateOrderStatus;

public class UpdateOrderStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/orders/{id:guid}/status", async (
            Guid id,
            UpdateOrderStatusRequest request,
            ISender sender) =>
        {
            var command = new UpdateOrderStatusCommand(id, request.Status, request.Reason);
            var result = await sender.Send(command);
            return Results.Ok(result);
        })
        .WithName("UpdateOrderStatus")
        .Produces<UpdateOrderStatusResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Order Status")
        .WithDescription("Update order status (admin only)")
        .WithTags("Orders");
    }
}

public record UpdateOrderStatusRequest(OrderStatus Status, string? Reason = null);
