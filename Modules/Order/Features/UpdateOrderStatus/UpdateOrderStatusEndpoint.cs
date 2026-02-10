using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace Order.Features.UpdateOrderStatus;

public record UpdateOrderStatusRequest(OrderStatus Status, string? Reason = null);
public record UpdateOrderStatusResponse(Guid OrderId, string OrderNumber, string PreviousStatus, string NewStatus, DateTime UpdatedAt);

public class UpdateOrderStatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/orders/{id}/status", async (
            [FromRoute] Guid id,
            [FromBody] UpdateOrderStatusRequest request,
            ISender sender) =>
        {
            var command = new UpdateOrderStatusCommand(id, request.Status, request.Reason);

            var result = await sender.Send(command);

            var response = result.Adapt<UpdateOrderStatusResponse>();

            return Results.Ok(response);
        })
        .Produces<UpdateOrderStatusResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithName("UpdateOrderStatus")
        .WithSummary("Update Order Status")
        .WithDescription("Update order status (admin only)")
        .WithTags("Orders");
    }
}
