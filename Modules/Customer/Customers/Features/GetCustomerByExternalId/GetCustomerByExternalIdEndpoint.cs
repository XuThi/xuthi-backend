namespace Customer.Customers.Features.GetCustomerByExternalId;

public record GetCustomerByExternalIdRequest(string ExternalId);
public record GetCustomerByExternalIdResponse(CustomerDetailDto? Customer);

// Endpoint
public class GetCustomerByExternalIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // GET /api/customers/by-external/{externalId}
        app.MapGet("/api/customers/by-external/{externalId}", async (
            [AsParameters] GetCustomerByExternalIdRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerByExternalIdQuery(request.ExternalId));
            var response = new GetCustomerByExternalIdResponse(result.Customer);
            return result.Customer is null ? Results.NotFound() : Results.Ok(response);
        })
        .Produces<GetCustomerByExternalIdResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Customers")
        .WithSummary("Get customer by external auth provider ID");
    }
}
