namespace Customer.Customers.Features.Addresses.DeleteAddress;

// Command and Result
public record DeleteAddressCommand(Guid AddressId) : ICommand<DeleteAddressResult>;
public record DeleteAddressResult(bool Success);

// Handler
internal class DeleteAddressHandler(CustomerDbContext db)
    : ICommandHandler<DeleteAddressCommand, DeleteAddressResult>
{
    public async Task<DeleteAddressResult> Handle(DeleteAddressCommand cmd, CancellationToken ct)
    {
        var address = await db.Addresses.FindAsync([cmd.AddressId], ct);
        if (address is null)
            return new DeleteAddressResult(false);

        var wasDefault = address.IsDefault;
        var customerId = address.CustomerId;

        db.Addresses.Remove(address);
        await db.SaveChangesAsync(ct);

        // If deleted was default, set another as default
        if (wasDefault)
        {
            var nextAddress = await db.Addresses
                .Where(a => a.CustomerId == customerId)
                .OrderBy(a => a.CreatedAt)
                .FirstOrDefaultAsync(ct);
            
            if (nextAddress is not null)
            {
                nextAddress.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }

        return new DeleteAddressResult(true);
    }
}
