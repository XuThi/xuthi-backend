namespace Customer.Customers.Features.Addresses.SetDefaultAddress;

public record SetDefaultAddressRouteRequest(Guid CustomerId, Guid AddressId);
public record SetDefaultAddressResponse(bool Success);

// Endpoint
public class SetDefaultAddressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // PATCH /api/customers/{customerId}/addresses/{addressId}/default
        app.MapPatch("/api/customers/{customerId:guid}/addresses/{addressId:guid}/default", async (
            [AsParameters] SetDefaultAddressRouteRequest route,
            ISender sender) =>
        {
            var result = await sender.Send(new SetDefaultAddressCommand(route.CustomerId, route.AddressId));
            var response = new SetDefaultAddressResponse(result.Success);
            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<SetDefaultAddressResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Customer Addresses")
        .WithSummary("Set address as default");
    }
}
