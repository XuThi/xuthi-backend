using Microsoft.Extensions.Logging;
using Order.Orders.OrderIntake;
using Order.Orders.Services;

namespace Order.Orders.Features.PayOsWebhook;

public record PayOsWebhookCommand(string RawPayload) : ICommand<PayOsWebhookResult>;
public record PayOsWebhookResult(
    bool Accepted = true,
    Guid? OrderId = null,
    PayOsPaymentResolution? Resolution = null);

internal class PayOsWebhookHandler(
    IPaymentService paymentService,
    IOrderIntake orderIntake,
    ILogger<PayOsWebhookHandler> logger)
    : ICommandHandler<PayOsWebhookCommand, PayOsWebhookResult>
{
    public async Task<PayOsWebhookResult> Handle(PayOsWebhookCommand command, CancellationToken cancellationToken)
    {
        var result = await paymentService.VerifyWebhookAsync(command.RawPayload, cancellationToken);
        var resolution = await orderIntake.ResolvePayOsPaymentResultAsync(result, cancellationToken);

        if (resolution.Resolution == PayOsPaymentResolution.LatePaidAfterGrace)
        {
            logger.LogWarning(
                "Late PayOS paid result after settlement grace for Order {OrderId} and PayOS order code {OrderCode}; manual review required.",
                resolution.OrderId,
                result.OrderCode);
        }

        return new PayOsWebhookResult(
            OrderId: resolution.OrderId,
            Resolution: resolution.Resolution);
    }
}
