namespace Promotion.SaleCampaigns.Features.UpdateSaleCampaignItem;

public record UpdateSaleCampaignItemRouteRequest(Guid ItemId);
public record UpdateSaleCampaignItemResponse(SaleCampaignItemResult Item);

public class UpdateSaleCampaignItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/sale-campaigns/items/{itemId:guid}", async (
            [AsParameters] UpdateSaleCampaignItemRouteRequest route,
            UpdateSaleCampaignItemRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateSaleCampaignItemCommand(route.ItemId, request));
            var response = new UpdateSaleCampaignItemResponse(result);
            return Results.Ok(response);
        })
        .WithName("UpdateSaleCampaignItem")
        .Produces<UpdateSaleCampaignItemResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Sale Campaign Item")
        .WithDescription("Update sale item pricing and stock limits")
        .WithTags("Sale Campaigns")
        .RequireAuthorization("Admin");
    }
}
