namespace Order.Features.Checkout;

public record CheckoutCommand(CheckoutRequest Request) : ICommand<CheckoutResult>;

public record CheckoutRequest(
    // Customer ID (optional - for logged in users)
    Guid? CustomerId,
    
    // Customer info
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    
    // Shipping
    string ShippingAddress,
    string ShippingCity,
    string ShippingDistrict,
    string ShippingWard,
    string? ShippingNote,
    
    // Payment
    PaymentMethod PaymentMethod,
    
    // Cart items
    List<CheckoutItem> Items,
    
    // Optional voucher
    string? VoucherCode
);

public record CheckoutItem(
    Guid ProductId,
    Guid VariantId,
    int Quantity
);

public record CheckoutResult(
    Guid OrderId,
    string OrderNumber,
    decimal Total,
    string Status
);

public class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(x => x.Request.CustomerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.CustomerPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.ShippingAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.ShippingCity).NotEmpty();
        RuleFor(x => x.Request.ShippingDistrict).NotEmpty();
        RuleFor(x => x.Request.ShippingWard).NotEmpty();
        RuleFor(x => x.Request.Items).NotEmpty().WithMessage("Cart cannot be empty");
        RuleForEach(x => x.Request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.VariantId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
