namespace Customer.Features.Addresses.AddAddress;

// Response
public record AddAddressResponse(Guid AddressId);

// Endpoint
public class AddAddressEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // POST /api/customers/{customerId}/addresses
        app.MapPost("/api/customers/{customerId:guid}/addresses", async (
            Guid customerId,
            AddAddressRequest request,
            ISender sender) =>
        {
            var result = await sender.Send(new AddAddressCommand(
                customerId,
                request.Label,
                request.RecipientName,
                request.Phone,
                request.Address,
                request.Ward,
                request.District,
                request.City,
                request.Note,
                request.SetAsDefault));
            return Results.Created($"/api/customers/{customerId}/addresses/{result.AddressId}", new AddAddressResponse(result.AddressId));
        })
        .Produces<AddAddressResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithTags("Customer Addresses")
        .WithSummary("Add new address")
        .RequireAuthorization("Authenticated");
    }
}
