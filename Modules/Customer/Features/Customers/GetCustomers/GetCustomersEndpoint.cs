namespace Customer.Features.Customers.GetCustomers;

public class GetCustomersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers", async ([AsParameters] GetCustomersQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithTags("Customers")
        .WithSummary("Get list of customers with pagination and search");
    }
}
