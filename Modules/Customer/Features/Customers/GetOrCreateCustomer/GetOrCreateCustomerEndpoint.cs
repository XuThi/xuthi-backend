namespace Customer.Features.Customers.GetOrCreateCustomer;

// Request
public record SyncCustomerRequest(string KeycloakUserId, string Email, string? FullName = null);

// Endpoint
public class GetOrCreateCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // POST /api/customers/sync - Get or create customer (called on login)
        app.MapPost("/api/customers/sync", async (SyncCustomerRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetOrCreateCustomerQuery(
                request.KeycloakUserId,
                request.Email,
                request.FullName));
            return Results.Ok(new { customer = result.Customer, isNew = result.IsNew });
        })
        .WithTags("Customers")
        .WithSummary("Sync customer profile on login")
        .WithDescription("Creates customer profile if doesn't exist, updates last login");
    }
}
