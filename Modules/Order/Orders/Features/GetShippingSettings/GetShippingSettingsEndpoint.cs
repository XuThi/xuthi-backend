using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Order.Orders.Features.GetShippingSettings;

public class GetShippingSettingsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/orders/shipping-settings", async (ISender sender) =>
        {
            var result = await sender.Send(new GetShippingSettingsQuery());
            return Results.Ok(result);
        })
        .WithName("GetShippingSettings")
        .WithTags("Orders")
        .WithSummary("Get active shipping fee settings");
    }
}
