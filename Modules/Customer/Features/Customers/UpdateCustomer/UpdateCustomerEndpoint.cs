namespace Customer.Features.Customers.UpdateCustomer;

// Endpoint
public class UpdateCustomerEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // PUT /api/customers/{id}
        app.MapPut("/api/customers/{id:guid}", async (Guid id, UpdateCustomerRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdateCustomerCommand(
                id,
                request.FullName,
                request.Phone,
                request.DateOfBirth,
                request.Gender,
                request.AcceptsMarketing,
                request.AcceptsSms));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Customers")
        .WithSummary("Update customer profile");
        // TODO: .RequireAuthorization()
    }
}
