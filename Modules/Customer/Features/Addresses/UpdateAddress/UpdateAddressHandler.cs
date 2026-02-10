using Customer.Infrastructure.Data;

namespace Customer.Features.Addresses.UpdateAddress;

// Request, Command and Result
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

public record UpdateAddressCommand(
    Guid AddressId,
    string Label,
    string RecipientName,
    string Phone,
    string Address,
    string Ward,
    string District,
    string City,
    string? Note,
    bool IsDefault) : ICommand<UpdateAddressResult>;

public record UpdateAddressResult(bool Success);

// Validator
public class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator()
    {
        RuleFor(x => x.AddressId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.RecipientName).NotEmpty();
    }
}

// Handler
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
