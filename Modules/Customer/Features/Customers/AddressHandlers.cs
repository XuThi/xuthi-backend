using Customer.Infrastructure.Data;
using Customer.Infrastructure.Entity;

namespace Customer.Features.Customers;

// TODO: Seperate all of this to different files

// Add address
internal class AddAddressHandler(CustomerDbContext db)
    : ICommandHandler<AddAddressCommand, AddAddressResult>
{
    public async Task<AddAddressResult> Handle(AddAddressCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == cmd.CustomerId, ct);

        if (customer is null)
            throw new InvalidOperationException($"Customer {cmd.CustomerId} not found");

        // If setting as default, clear other defaults
        if (cmd.SetAsDefault || customer.Addresses.Count == 0)
        {
            foreach (var addr in customer.Addresses)
                addr.IsDefault = false;
        }

        var address = new CustomerAddress
        {
            Id = Guid.NewGuid(),
            CustomerId = cmd.CustomerId,
            Label = cmd.Label,
            RecipientName = cmd.RecipientName,
            Phone = cmd.Phone,
            Address = cmd.Address,
            Ward = cmd.Ward,
            District = cmd.District,
            City = cmd.City,
            Note = cmd.Note,
            IsDefault = cmd.SetAsDefault || customer.Addresses.Count == 0
        };

        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);

        return new AddAddressResult(address.Id);
    }
}

// Update address
internal class UpdateAddressHandler(CustomerDbContext db)
    : ICommandHandler<UpdateAddressCommand, UpdateAddressResult>
{
    public async Task<UpdateAddressResult> Handle(UpdateAddressCommand cmd, CancellationToken ct)
    {
        var address = await db.Addresses.FindAsync([cmd.AddressId], ct);
        if (address is null)
            return new UpdateAddressResult(false);

        // If setting as default, clear other defaults
        if (cmd.IsDefault && !address.IsDefault)
        {
            var otherAddresses = await db.Addresses
                .Where(a => a.CustomerId == address.CustomerId && a.Id != address.Id)
                .ToListAsync(ct);
            foreach (var a in otherAddresses)
                a.IsDefault = false;
        }

        address.Label = cmd.Label;
        address.RecipientName = cmd.RecipientName;
        address.Phone = cmd.Phone;
        address.Address = cmd.Address;
        address.Ward = cmd.Ward;
        address.District = cmd.District;
        address.City = cmd.City;
        address.Note = cmd.Note;
        address.IsDefault = cmd.IsDefault;
        address.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new UpdateAddressResult(true);
    }
}

// Delete address
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

// Set default address
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
