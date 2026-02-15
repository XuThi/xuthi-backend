namespace Customer.Features.Addresses.UpdateAddress;

public record UpdateAddressRouteRequest(Guid CustomerId, Guid AddressId);
public record UpdateAddressResponse(bool Success);

// Endpoint
public class UpdateAddressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // PUT /api/customers/{customerId}/addresses/{addressId}
        app.MapPut("/api/customers/{customerId:guid}/addresses/{addressId:guid}", async (
            [AsParameters] UpdateAddressRouteRequest route,
            UpdateAddressRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new UpdateAddressCommand(
                route.AddressId,
                request.Label,
                request.RecipientName,
                request.Phone,
                request.Address,
                request.Ward,
                request.District,
                request.City,
                request.Note,
                request.IsDefault));

            var response = new UpdateAddressResponse(result.Success);

            return result.Success ? Results.Ok(response) : Results.NotFound();
        })
        .Produces<UpdateAddressResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Customer Addresses")
        .WithSummary("Update address");
    }
}
