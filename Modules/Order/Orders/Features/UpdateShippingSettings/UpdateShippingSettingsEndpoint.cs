using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Order.Orders.Features.UpdateShippingSettings;

public class UpdateShippingSettingsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders/shipping-settings", async (
            UpdateShippingSettingsCommand command,
            ISender sender) =>
        {
            var result = await sender.Send(command);
            return result ? Results.Ok(new { success = true }) : Results.BadRequest("Failed to update settings");
        })
        .WithName("UpdateShippingSettings")
        .WithTags("Orders")
        .RequireAuthorization("Staff")
        .WithSummary("Update shipping fee settings (requires Staff/Admin role)");
    }
}
