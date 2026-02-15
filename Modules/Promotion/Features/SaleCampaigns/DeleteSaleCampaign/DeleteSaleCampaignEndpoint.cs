namespace Promotion.Features.SaleCampaigns.DeleteSaleCampaign;

public record DeleteSaleCampaignRouteRequest(Guid Id);
public record DeleteSaleCampaignResponse(bool Success);

public class DeleteSaleCampaignEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/sale-campaigns/{id:guid}", async (
            [AsParameters] DeleteSaleCampaignRouteRequest route,
            ISender sender) =>
        {
            var success = await sender.Send(new DeleteSaleCampaignCommand(route.Id));
            var response = new DeleteSaleCampaignResponse(success);
            return success ? Results.Ok(response) : Results.NotFound();
        })
        .WithName("DeleteSaleCampaign")
        .Produces<DeleteSaleCampaignResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Delete Sale Campaign")
        .WithDescription("Delete sale campaign and all its items")
        .WithTags("Sale Campaigns")
        .RequireAuthorization("Admin");
    }
}
