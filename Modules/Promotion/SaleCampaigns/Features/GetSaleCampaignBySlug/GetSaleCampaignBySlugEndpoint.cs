namespace Promotion.SaleCampaigns.Features.GetSaleCampaignBySlug;

public record GetSaleCampaignBySlugRequest(string Slug);
public record GetSaleCampaignBySlugResponse(SaleCampaignDetailResult Campaign);

public class GetSaleCampaignBySlugEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sale-campaigns/by-slug/{slug}", async (
            [AsParameters] GetSaleCampaignBySlugRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetSaleCampaignBySlugQuery(request.Slug));
            var response = new GetSaleCampaignBySlugResponse(result);
            return Results.Ok(response);
        })
        .WithName("GetSaleCampaignBySlug")
        .Produces<GetSaleCampaignBySlugResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Get Sale Campaign by Slug")
        .WithDescription("Get sale campaign by URL slug")
        .WithTags("Sale Campaigns");
    }
}
