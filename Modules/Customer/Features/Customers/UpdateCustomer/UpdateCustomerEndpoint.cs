namespace Customer.Features.Customers.UpdateCustomer;

public record UpdateCustomerRouteRequest(Guid Id);
public record UpdateCustomerResponse(bool Success);

// Endpoint
public class UpdateCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // PUT /api/customers/{id}
        app.MapPut("/api/customers/{id:guid}", async (
            [AsParameters] UpdateCustomerRouteRequest route,
            UpdateCustomerRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateCustomerCommand(
                route.Id,
                request.FullName,
                request.Phone,
                request.DateOfBirth,
                request.Gender,
                request.AcceptsMarketing,
                request.AcceptsSms));

            var response = new UpdateCustomerResponse(result.Success);

            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<UpdateCustomerResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Customers")
        .WithSummary("Update customer profile")
        .RequireAuthorization("Authenticated");
    }
}
