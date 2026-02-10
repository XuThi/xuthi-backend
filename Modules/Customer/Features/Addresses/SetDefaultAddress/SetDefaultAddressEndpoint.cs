namespace Customer.Features.Addresses.SetDefaultAddress;

// Endpoint
public class SetDefaultAddressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // PATCH /api/customers/{customerId}/addresses/{addressId}/default
        app.MapPatch("/api/customers/{customerId:guid}/addresses/{addressId:guid}/default", async (
            Guid customerId,
            Guid addressId,
            ISender sender) =>
        {
            var result = await sender.Send(new SetDefaultAddressCommand(customerId, addressId));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Customer Addresses")
        .WithSummary("Set address as default");
    }
}
