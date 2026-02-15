namespace Promotion.Features.SaleCampaigns.RemoveSaleCampaignItem;

public record RemoveSaleCampaignItemRouteRequest(Guid ItemId);
public record RemoveSaleCampaignItemResponse(bool Success);

public class RemoveSaleCampaignItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/sale-campaigns/items/{itemId:guid}", async (
            [AsParameters] RemoveSaleCampaignItemRouteRequest route,
            ISender sender) =>
        {
            var success = await sender.Send(new RemoveSaleCampaignItemCommand(route.ItemId));
            var response = new RemoveSaleCampaignItemResponse(success);
            return success ? Results.Ok(response) : Results.NotFound();
        })
        .WithName("RemoveSaleCampaignItem")
        .Produces<RemoveSaleCampaignItemResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Remove Item from Sale Campaign")
        .WithDescription("Remove a product from a sale campaign")
        .WithTags("Sale Campaigns")
        .RequireAuthorization("Admin");
    }
}
