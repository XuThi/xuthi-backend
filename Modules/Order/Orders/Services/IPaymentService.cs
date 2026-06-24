namespace Order.Orders.Services;

using PayOS.Models.Webhooks;

public interface IPaymentService
{
    /// <summary>
    /// Create a payment link for an order. Returns the checkout URL.
    /// </summary>
    Task<PaymentLinkResult> CreatePaymentLinkAsync(
        CustomerOrder order,
        string returnUrl,
        string cancelUrl,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);

    /// <summary>
    /// Verify and handle incoming webhook payload from PayOS. Returns the order code and whether payment succeeded.
    /// </summary>
    Task<WebhookResult> HandleWebhookAsync(Webhook webhookPayload, CancellationToken ct = default);
}

public record PaymentLinkResult(string CheckoutUrl, long OrderCode, string? PaymentLinkId = null);
public record WebhookResult(long OrderCode, bool IsSuccess, string Status);
