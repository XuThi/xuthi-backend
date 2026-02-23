namespace Promotion.SaleCampaigns.Features.AddSaleCampaignItem;

public record AddSaleCampaignItemRouteRequest(Guid CampaignId);
public record AddSaleCampaignItemResponse(SaleCampaignItemResult Item);

public class AddSaleCampaignItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sale-campaigns/{campaignId:guid}/items", async (
            [AsParameters] AddSaleCampaignItemRouteRequest route,
            CreateSaleCampaignItemRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new AddSaleCampaignItemCommand(route.CampaignId, request));
            var response = new AddSaleCampaignItemResponse(result);
            return Results.Created($"/api/sale-campaigns/{route.CampaignId}/items/{result.Id}", response);
        })
        .WithName("AddSaleCampaignItem")
        .Produces<AddSaleCampaignItemResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Add Item to Sale Campaign")
        .WithDescription("Add a product/variant to a sale campaign with discount pricing")
        .WithTags("Sale Campaigns")
        .RequireAuthorization("Admin");
    }
}
