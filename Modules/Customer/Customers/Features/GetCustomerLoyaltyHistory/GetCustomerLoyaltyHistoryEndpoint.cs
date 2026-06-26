namespace Customer.Customers.Features.GetCustomerLoyaltyHistory;

public record GetCustomerLoyaltyHistoryResponse(IReadOnlyList<LoyaltyHistoryDto> History);

public sealed class GetCustomerLoyaltyHistoryEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers/{customerId:guid}/loyalty-history", async (
            Guid customerId,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerLoyaltyHistoryQuery(customerId));
            return Results.Ok(new GetCustomerLoyaltyHistoryResponse(result.History));
        })
        .WithName("GetCustomerLoyaltyHistory")
        .Produces<GetCustomerLoyaltyHistoryResponse>(StatusCodes.Status200OK)
        .WithSummary("Get Customer Loyalty History")
        .WithDescription("Gets Customer Loyalty audit history ordered by occurrence time.");
    }
}
