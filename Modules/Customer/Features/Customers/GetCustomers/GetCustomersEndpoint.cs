using Mapster;

namespace Customer.Features.Customers.GetCustomers;

public record GetCustomersRequest(int Page = 1, int PageSize = 10, string? Search = null);
public record GetCustomersResponse(IEnumerable<CustomerDto> Customers, long TotalCount);

public class GetCustomersEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers", async ([AsParameters] GetCustomersRequest request, ISender sender) =>
        {
            var query = request.Adapt<GetCustomersQuery>();
            var result = await sender.Send(query);
            var response = result.Adapt<GetCustomersResponse>();
            return Results.Ok(response);
        })
        .Produces<GetCustomersResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .WithTags("Customers")
        .WithSummary("Get list of customers with pagination and search");
    }
}
