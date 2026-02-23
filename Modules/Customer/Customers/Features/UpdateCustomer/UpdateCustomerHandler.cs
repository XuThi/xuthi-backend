namespace Customer.Customers.Features.UpdateCustomer;

// Request, Command, Result
public record UpdateCustomerRequest(
    string? FullName,
    string? Phone,
    DateTime? DateOfBirth,
    Gender? Gender,
    bool? AcceptsMarketing,
    bool? AcceptsSms);

public record UpdateCustomerCommand(
    Guid Id,
    string? FullName,
    string? Phone,
    DateTime? DateOfBirth,
    Gender? Gender,
    bool? AcceptsMarketing,
    bool? AcceptsSms) : ICommand<UpdateCustomerResult>;

public record UpdateCustomerResult(bool Success);

// Validator
public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");
    }
}

// Handler
internal class UpdateCustomerHandler(CustomerDbContext db)
    : ICommandHandler<UpdateCustomerCommand, UpdateCustomerResult>
{
    public async Task<UpdateCustomerResult> Handle(UpdateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([cmd.Id], ct);
        if (customer is null)
            return new UpdateCustomerResult(false);

        if (cmd.FullName is not null) customer.FullName = cmd.FullName;
        if (cmd.Phone is not null) customer.Phone = cmd.Phone;
        if (cmd.DateOfBirth.HasValue) customer.DateOfBirth = cmd.DateOfBirth;
        if (cmd.Gender.HasValue) customer.Gender = cmd.Gender;
        if (cmd.AcceptsMarketing.HasValue) customer.AcceptsMarketing = cmd.AcceptsMarketing.Value;
        if (cmd.AcceptsSms.HasValue) customer.AcceptsSms = cmd.AcceptsSms.Value;
        
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new UpdateCustomerResult(true);
    }
}
