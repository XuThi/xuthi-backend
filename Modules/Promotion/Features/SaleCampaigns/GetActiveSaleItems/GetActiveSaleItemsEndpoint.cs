namespace Promotion.Features.SaleCampaigns.GetActiveSaleItems;

public record GetActiveSaleItemsRequest(string? ProductIds, string? VariantIds);
public record GetActiveSaleItemsResponse(List<ActiveSaleItemResult> Items);

public class GetActiveSaleItemsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sale-campaigns/active-items", async (
            [AsParameters] GetActiveSaleItemsRequest request,
            ISender sender) =>
        {
            var productIds = ParseIds(request.ProductIds);
            var variantIds = ParseIds(request.VariantIds);

            var result = await sender.Send(new GetActiveSaleItemsQuery(productIds, variantIds));
            return Results.Ok(new GetActiveSaleItemsResponse(result.Items));
        })
        .Produces<GetActiveSaleItemsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("SaleCampaigns")
        .WithSummary("Get active sale items")
        .WithDescription("Returns items from active, running sale campaigns by product or variant IDs");
    }

    private static List<Guid> ParseIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var ids = new List<Guid>();
        foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(token, out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
