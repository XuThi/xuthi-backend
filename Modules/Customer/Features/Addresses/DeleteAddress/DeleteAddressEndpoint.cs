namespace Customer.Features.Addresses.DeleteAddress;

// Endpoint
public class DeleteAddressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // DELETE /api/customers/{customerId}/addresses/{addressId}
        app.MapDelete("/api/customers/{customerId:guid}/addresses/{addressId:guid}", async (
            Guid customerId,
            Guid addressId,
            ISender sender) =>
        {
            var result = await sender.Send(new DeleteAddressCommand(addressId));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Customer Addresses")
        .WithSummary("Delete address");
    }
}
