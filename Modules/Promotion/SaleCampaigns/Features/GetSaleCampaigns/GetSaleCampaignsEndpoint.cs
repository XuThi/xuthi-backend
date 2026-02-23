namespace Promotion.SaleCampaigns.Features.GetSaleCampaigns;

public record GetSaleCampaignsRequest(
    bool? IsActive = null,
    bool? IsFeatured = null,
    SaleCampaignType? Type = null,
    bool? OnlyRunning = null,
    bool? OnlyUpcoming = null,
    int Page = 1,
    int PageSize = 20
);

public record GetSaleCampaignsResponse(SaleCampaignsResult Campaigns);

public class GetSaleCampaignsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sale-campaigns", async (
            [AsParameters] GetSaleCampaignsRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetSaleCampaignsQuery(
                request.IsActive, request.IsFeatured, request.Type,
                request.OnlyRunning, request.OnlyUpcoming,
                request.Page, request.PageSize));
            var response = new GetSaleCampaignsResponse(result);
            return Results.Ok(response);
        })
        .WithName("GetSaleCampaigns")
        .Produces<GetSaleCampaignsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Get Sale Campaigns")
        .WithDescription("Get paginated list of sale campaigns with optional filters")
        .WithTags("Sale Campaigns");
    }
}
