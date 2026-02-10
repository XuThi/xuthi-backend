using Customer.Infrastructure.Data;
using Customer.Infrastructure.Entity;

namespace Customer.Features.Addresses.AddAddress;

// Request, Command and Result
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

public record AddAddressCommand(
    Guid CustomerId,
    string Label,
    string RecipientName,
    string Phone,
    string Address,
    string Ward,
    string District,
    string City,
    string? Note,
    bool SetAsDefault = false) : ICommand<AddAddressResult>;

public record AddAddressResult(Guid AddressId);

// Validator
public class AddAddressCommandValidator : AbstractValidator<AddAddressCommand>
{
    public AddAddressCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.RecipientName).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.Address).NotEmpty();
    }
}

// Handler
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
