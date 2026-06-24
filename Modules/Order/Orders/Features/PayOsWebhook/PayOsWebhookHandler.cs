using Order.Orders.OrderIntake;
using Order.Orders.Services;

namespace Order.Orders.Features.PayOsWebhook;

public record PayOsWebhookCommand(string RawPayload) : ICommand<PayOsWebhookResult>;
public record PayOsWebhookResult(bool Accepted = true);

internal class PayOsWebhookHandler(
    IPaymentService paymentService,
    IOrderIntake orderIntake)
    : ICommandHandler<PayOsWebhookCommand, PayOsWebhookResult>
{
    public async Task<PayOsWebhookResult> Handle(PayOsWebhookCommand command, CancellationToken cancellationToken)
    {
        var result = await paymentService.VerifyWebhookAsync(command.RawPayload, cancellationToken);
        await orderIntake.ResolvePayOsPaymentResultAsync(result, cancellationToken);

        return new PayOsWebhookResult();
    }
}
