namespace Customer.Features.Customers.GetOrCreateCustomer;

// Request
public record SyncCustomerRequest(string ExternalUserId, string Email, string? FullName = null);
public record SyncCustomerResponse(CustomerDto Customer, bool IsNew);

// Endpoint
public class GetOrCreateCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // POST /api/customers/sync - Get or create customer (called on login)
        app.MapPost("/api/customers/sync", async (SyncCustomerRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetOrCreateCustomerQuery(
                request.ExternalUserId,
                request.Email,
                request.FullName));
            var response = new SyncCustomerResponse(result.Customer, result.IsNew);
            return Results.Ok(response);
        })
        .Produces<SyncCustomerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Customers")
        .WithSummary("Sync customer profile on login")
        .WithDescription("Creates customer profile if doesn't exist, updates last login");
    }
}
