using Mapster;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Order.Orders.Features.CancelPendingPayOsOrder;

public record CancelPendingPayOsOrderRequest(string? Reason = null);
public record CancelPendingPayOsOrderResponse(
    Guid OrderId,
    string OrderNumber,
    string Status,
    string PaymentStatus,
    DateTime CancelledAt,
    string? CancellationReason
);

public class CancelPendingPayOsOrderEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders/{id:guid}/cancel-payment", async (
            [FromRoute] Guid id,
            [FromBody] CancelPendingPayOsOrderRequest request,
            ClaimsPrincipal principal,
            ISender sender) =>
        {
            var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? userId = Guid.TryParse(userIdRaw, out var parsedUserId) ? parsedUserId : null;
            var email = principal.FindFirstValue(ClaimTypes.Email);

            if (userId is null && string.IsNullOrWhiteSpace(email))
                return Results.Unauthorized();

            var result = await sender.Send(new CancelPendingPayOsOrderCommand(
                id,
                userId,
                email,
                request.Reason));

            var response = result.Adapt<CancelPendingPayOsOrderResponse>();
            return Results.Ok(response);
        })
        .WithName("CancelPendingPayOsOrder")
        .Produces<CancelPendingPayOsOrderResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Cancel pending PayOS order")
        .WithDescription("Cancel only pending PayOS orders that belong to the authenticated customer")
        .WithTags("Orders")
        .RequireAuthorization("Authenticated");
    }
}
