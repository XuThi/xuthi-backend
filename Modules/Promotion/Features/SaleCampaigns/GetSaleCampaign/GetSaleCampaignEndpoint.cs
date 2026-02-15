namespace Promotion.Features.SaleCampaigns.GetSaleCampaign;

public record GetSaleCampaignRequest(Guid Id);
public record GetSaleCampaignResponse(SaleCampaignDetailResult Campaign);

public class GetSaleCampaignEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sale-campaigns/{id:guid}", async (
            [AsParameters] GetSaleCampaignRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetSaleCampaignQuery(request.Id));
            var response = new GetSaleCampaignResponse(result);
            return Results.Ok(response);
        })
        .WithName("GetSaleCampaign")
        .Produces<GetSaleCampaignResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Sale Campaign")
        .WithDescription("Get sale campaign by ID with all items")
        .WithTags("Sale Campaigns");
    }
}
