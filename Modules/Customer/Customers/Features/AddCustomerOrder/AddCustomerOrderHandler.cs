namespace Customer.Customers.Features.AddCustomerOrder;

// Command and Result
public record AddCustomerOrderCommand(
    Guid CustomerId,
    decimal OrderTotal,
    int PointsEarned,
    Guid OrderId) : ICommand<AddCustomerOrderResult>;

public record AddCustomerOrderResult(CustomerTier NewTier, int TotalPoints);

// Validator
public class AddCustomerOrderCommandValidator : AbstractValidator<AddCustomerOrderCommand>
{
    public AddCustomerOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.OrderTotal).GreaterThan(0);
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

// Handler
internal class AddCustomerOrderHandler
    : ICommandHandler<AddCustomerOrderCommand, AddCustomerOrderResult>
{
    public Task<AddCustomerOrderResult> Handle(AddCustomerOrderCommand cmd, CancellationToken ct)
    {
        throw new InvalidOperationException(
            "AddCustomerOrderCommand is retired. Customer Loyalty awards are recorded from Delivered Customer Order Outcomes.");
    }
}
