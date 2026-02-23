namespace Promotion.SaleCampaigns.Features.CreateSaleCampaign;

public record CreateSaleCampaignRequest(
    string Name,
    string? Description,
    string? BannerImageUrl,
    SaleCampaignType Type,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive = true,
    bool IsFeatured = false,
    List<CreateSaleCampaignItemRequest>? Items = null
);

public record CreateSaleCampaignResponse(
    Guid Id, string Name, string? Slug, string? Description, string? BannerImageUrl,
    SaleCampaignType Type, DateTime StartDate, DateTime EndDate,
    bool IsActive, bool IsFeatured, bool IsRunning, bool IsUpcoming, int ItemCount
);

public class CreateSaleCampaignEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sale-campaigns", async (CreateSaleCampaignRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateSaleCampaignCommand(request));
            var response = new CreateSaleCampaignResponse(
                result.Id, result.Name, result.Slug, result.Description, result.BannerImageUrl,
                result.Type, result.StartDate, result.EndDate, result.IsActive, result.IsFeatured,
                result.IsRunning, result.IsUpcoming, result.ItemCount);
            return Results.Created($"/api/sale-campaigns/{result.Id}", response);
        })
        .WithName("CreateSaleCampaign")
        .Produces<CreateSaleCampaignResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithSummary("Create Sale Campaign")
        .WithDescription("Create a new sale campaign (flash sale, seasonal sale, clearance, etc.)")
        .WithTags("Sale Campaigns")
        .RequireAuthorization("Admin");
    }
}
