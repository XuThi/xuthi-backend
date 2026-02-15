namespace Customer.Features.Addresses.DeleteAddress;

public record DeleteAddressRouteRequest(Guid CustomerId, Guid AddressId);
public record DeleteAddressResponse(bool Success);

// Endpoint
public class DeleteAddressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // DELETE /api/customers/{customerId}/addresses/{addressId}
        app.MapDelete("/api/customers/{customerId:guid}/addresses/{addressId:guid}", async (
            [AsParameters] DeleteAddressRouteRequest route,
            ISender sender) =>
        {
            var result = await sender.Send(new DeleteAddressCommand(route.AddressId));
            var response = new DeleteAddressResponse(result.Success);
            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<DeleteAddressResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Customer Addresses")
        .WithSummary("Delete address");
    }
}
