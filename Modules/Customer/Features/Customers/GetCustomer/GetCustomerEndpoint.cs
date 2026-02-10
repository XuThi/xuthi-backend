namespace Customer.Features.Customers.GetCustomer;

// Endpoint
public class GetCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /api/customers/{id}
        app.MapGet("/api/customers/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerQuery(id));
            return result.Customer is null ? Results.NotFound() : Results.Ok(result.Customer);
        })
        .WithTags("Customers")
        .WithSummary("Get customer by ID");
        // TODO: .RequireAuthorization()
    }
}
