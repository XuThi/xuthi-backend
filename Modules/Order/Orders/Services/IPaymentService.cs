namespace Order.Orders.Services;

public interface IPaymentService
{
    /// <summary>
    /// Create a payment link for an order. Returns the checkout URL.
    /// </summary>
    Task<PaymentLinkResult> CreatePaymentLinkAsync(
        CustomerOrder order,
        string returnUrl,
        string cancelUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Handle incoming webhook payload from PayOS. Returns the order code and whether payment succeeded.
    /// </summary>
    Task<WebhookResult> HandleWebhookAsync(string webhookBody, string signature, CancellationToken ct = default);
}

public record PaymentLinkResult(string CheckoutUrl, long OrderCode);
public record WebhookResult(long OrderCode, bool IsSuccess, string Status);
