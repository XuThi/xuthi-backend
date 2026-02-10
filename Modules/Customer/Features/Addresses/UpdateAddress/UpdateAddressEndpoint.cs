namespace Customer.Features.Addresses.UpdateAddress;

// Endpoint
public class UpdateAddressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // PUT /api/customers/{customerId}/addresses/{addressId}
        app.MapPut("/api/customers/{customerId:guid}/addresses/{addressId:guid}", async (
            Guid customerId,
            Guid addressId,
            UpdateAddressRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateAddressCommand(
                addressId,
                request.Label,
                request.RecipientName,
                request.Phone,
                request.Address,
                request.Ward,
                request.District,
                request.City,
                request.Note,
                request.IsDefault));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithTags("Customer Addresses")
        .WithSummary("Update address");
    }
}
