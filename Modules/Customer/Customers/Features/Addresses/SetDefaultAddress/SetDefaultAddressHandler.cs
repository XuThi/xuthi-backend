namespace Customer.Customers.Features.Addresses.SetDefaultAddress;

// Command and Result
public record SetDefaultAddressCommand(Guid CustomerId, Guid AddressId) : ICommand<SetDefaultAddressResult>;
public record SetDefaultAddressResult(bool Success);

// Handler
internal class SetDefaultAddressHandler(CustomerDbContext db)
    : ICommandHandler<SetDefaultAddressCommand, SetDefaultAddressResult>
{
    public async Task<SetDefaultAddressResult> Handle(SetDefaultAddressCommand cmd, CancellationToken ct)
    {
        var addresses = await db.Addresses
            .Where(a => a.CustomerId == cmd.CustomerId)
            .ToListAsync(ct);

        var targetAddress = addresses.FirstOrDefault(a => a.Id == cmd.AddressId);
        if (targetAddress is null)
            return new SetDefaultAddressResult(false);

        foreach (var addr in addresses)
            addr.IsDefault = addr.Id == cmd.AddressId;

        await db.SaveChangesAsync(ct);
        return new SetDefaultAddressResult(true);
    }
}
