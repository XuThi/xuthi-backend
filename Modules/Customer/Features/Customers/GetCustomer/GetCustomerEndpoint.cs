namespace Customer.Features.Customers.GetCustomer;

public record GetCustomerRequest(Guid Id);
public record GetCustomerResponse(CustomerDetailDto? Customer);

// Endpoint
public class GetCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /api/customers/{id}
        app.MapGet("/api/customers/{id:guid}", async (
            [AsParameters] GetCustomerRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerQuery(request.Id));
            var response = new GetCustomerResponse(result.Customer);

            return result.Customer is null ? Results.NotFound() : Results.Ok(response);
        })
        .Produces<GetCustomerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Customers")
        .WithSummary("Get customer by ID")
        .RequireAuthorization("Authenticated");
    }
}
