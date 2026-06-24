namespace Order.Orders.Services;

using Order.Orders.OrderIntake;

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
    /// Verify an incoming PayOS webhook payload and return its domain-shaped payment result.
    /// </summary>
    Task<PayOsPaymentResult> VerifyWebhookAsync(string rawPayload, CancellationToken ct = default);
}

public record PaymentLinkResult(string CheckoutUrl, long OrderCode, string? PaymentLinkId = null);
