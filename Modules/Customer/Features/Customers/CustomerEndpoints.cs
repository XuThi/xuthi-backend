using Customer.Infrastructure.Entity;

namespace Customer.Features.Customers;

// TODO: Seperate all of this

public class CustomerEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customers");

        // GET /api/customers/{id}
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerQuery(id));
            return result.Customer is null ? Results.NotFound() : Results.Ok(result.Customer);
        })
        .WithSummary("Get customer by ID");
        // TODO: .RequireAuthorization()

        // GET /api/customers/me - Get current user's customer profile
        // This would use claims from JWT to get the Keycloak user ID
        group.MapGet("/by-keycloak/{keycloakId}", async (string keycloakId, ISender sender) =>
        {
            var result = await sender.Send(new GetCustomerByKeycloakIdQuery(keycloakId));
            return result.Customer is null ? Results.NotFound() : Results.Ok(result.Customer);
        })
        .WithSummary("Get customer by Keycloak ID");
        // TODO: .RequireAuthorization()

        // POST /api/customers/sync - Get or create customer (called on login)
        group.MapPost("/sync", async (SyncCustomerRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetOrCreateCustomerQuery(
                request.KeycloakUserId,
                request.Email,
                request.FullName));
            return Results.Ok(new { customer = result.Customer, isNew = result.IsNew });
        })
        .WithSummary("Sync customer profile on login")
        .WithDescription("Creates customer profile if doesn't exist, updates last login");

        // PUT /api/customers/{id}
        group.MapPut("/{id:guid}", async (Guid id, UpdateCustomerRequest request, ISender sender) =>
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
        .WithSummary("Update customer profile");
        // TODO: .RequireAuthorization()

        // === Address Management ===
        var addressGroup = app.MapGroup("/api/customers/{customerId:guid}/addresses")
            .WithTags("Customer Addresses");

        // POST /api/customers/{customerId}/addresses
        addressGroup.MapPost("/", async (Guid customerId, AddAddressRequest request, ISender sender) =>
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
            return Results.Created($"/api/customers/{customerId}/addresses/{result.AddressId}", result);
        })
        .WithSummary("Add new address");

        // PUT /api/customers/{customerId}/addresses/{addressId}
        addressGroup.MapPut("/{addressId:guid}", async (
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
        .WithSummary("Update address");

        // DELETE /api/customers/{customerId}/addresses/{addressId}
        addressGroup.MapDelete("/{addressId:guid}", async (
            Guid customerId,
            Guid addressId,
            ISender sender) =>
        {
            var result = await sender.Send(new DeleteAddressCommand(addressId));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithSummary("Delete address");

        // PATCH /api/customers/{customerId}/addresses/{addressId}/default
        addressGroup.MapPatch("/{addressId:guid}/default", async (
            Guid customerId,
            Guid addressId,
            ISender sender) =>
        {
            var result = await sender.Send(new SetDefaultAddressCommand(customerId, addressId));
            return result.Success ? Results.NoContent() : Results.NotFound();
        })
        .WithSummary("Set address as default");
    }
}

// Request DTOs
public record SyncCustomerRequest(string KeycloakUserId, string Email, string? FullName = null);

public record UpdateCustomerRequest(
    string? FullName,
    string? Phone,
    DateTime? DateOfBirth,
    Gender? Gender,
    bool? AcceptsMarketing,
    bool? AcceptsSms);

public record AddAddressRequest(
    string Label,
    string RecipientName,
    string Phone,
    string Address,
    string Ward,
    string District,
    string City,
    string? Note = null,
    bool SetAsDefault = false);

public record UpdateAddressRequest(
    string Label,
    string RecipientName,
    string Phone,
    string Address,
    string Ward,
    string District,
    string City,
    string? Note,
    bool IsDefault);
