namespace Promotion.SaleCampaigns.Features.UpdateSaleCampaign;

public record UpdateSaleCampaignRequest(
    string? Name = null,
    string? Description = null,
    string? BannerImageUrl = null,
    SaleCampaignType? Type = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    bool? IsActive = null,
    bool? IsFeatured = null
);

public record UpdateSaleCampaignRouteRequest(Guid Id);

public record UpdateSaleCampaignResponse(
    Guid Id, string Name, string? Slug, string? Description, string? BannerImageUrl,
    SaleCampaignType Type, DateTime StartDate, DateTime EndDate,
    bool IsActive, bool IsFeatured, bool IsRunning, bool IsUpcoming, int ItemCount
);

public class UpdateSaleCampaignEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/sale-campaigns/{id:guid}", async (
            [AsParameters] UpdateSaleCampaignRouteRequest route,
            UpdateSaleCampaignRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateSaleCampaignCommand(route.Id, request));
            var response = new UpdateSaleCampaignResponse(
                result.Id, result.Name, result.Slug, result.Description, result.BannerImageUrl,
                result.Type, result.StartDate, result.EndDate, result.IsActive, result.IsFeatured,
                result.IsRunning, result.IsUpcoming, result.ItemCount);
            return Results.Ok(response);
        })
        .WithName("UpdateSaleCampaign")
        .Produces<UpdateSaleCampaignResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Update Sale Campaign")
        .WithDescription("Update sale campaign details")
        .WithTags("Sale Campaigns")
        .RequireAuthorization("Admin");
    }
}
