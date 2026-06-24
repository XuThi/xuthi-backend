using Order.Orders.OrderIntake;

namespace Order.Orders.Features.Checkout;

public record CheckoutCommand(CheckoutRequest Request) : ICommand<CheckoutResult>;
public record CheckoutResult(Guid OrderId, string OrderNumber, decimal Total, string Status, string? PaymentUrl = null);

public class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(x => x.Request.CartId).NotEmpty();
        RuleFor(x => x.Request.CustomerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.CustomerPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.ShippingAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.ShippingCity).NotEmpty();
        RuleFor(x => x.Request.ShippingWard);
        RuleFor(x => x.Request.ReturnUrl)
            .NotEmpty()
            .When(x => x.Request.PaymentMethod == PaymentMethod.PayOS);
        RuleFor(x => x.Request.CancelUrl)
            .NotEmpty()
            .When(x => x.Request.PaymentMethod == PaymentMethod.PayOS);
    }
}

internal class CheckoutHandler(IOrderIntake orderIntake)
    : ICommandHandler<CheckoutCommand, CheckoutResult>
{
    public async Task<CheckoutResult> Handle(CheckoutCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        var result = await orderIntake.StartOrderAttemptAsync(new StartOrderAttempt(
            req.CartId,
            req.CustomerId,
            req.CustomerName,
            req.CustomerEmail,
            req.CustomerPhone,
            req.ShippingAddress,
            req.ShippingCity,
            req.ShippingWard,
            req.ShippingNote,
            req.PaymentMethod,
            req.ReturnUrl,
            req.CancelUrl,
            req.ShippingDistrict), cancellationToken);

        return new CheckoutResult(
            result.OrderId,
            result.OrderNumber,
            result.Total,
            result.Status,
            result.PaymentUrl);
    }
}
