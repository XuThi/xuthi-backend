using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace Order.Orders.Services;

public class PayOsPaymentService : IPaymentService
{
    private readonly PayOSClient _client;
    private readonly ILogger<PayOsPaymentService> _logger;

    public PayOsPaymentService(
        IConfiguration configuration,
        ILogger<PayOsPaymentService> logger)
    {
        _logger = logger;

        var clientId = configuration["PayOS:ClientId"]
            ?? throw new InvalidOperationException("PayOS:ClientId is not configured");
        var apiKey = configuration["PayOS:ApiKey"]
            ?? throw new InvalidOperationException("PayOS:ApiKey is not configured");
        var checksumKey = configuration["PayOS:ChecksumKey"]
            ?? throw new InvalidOperationException("PayOS:ChecksumKey is not configured");

        _client = new PayOSClient(clientId, apiKey, checksumKey);
    }

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(
        CustomerOrder order,
        string returnUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        // PayOS orderCode must be a positive integer (Int64).
        // Generate a unique code from the order timestamp + random suffix
        var orderCode = GenerateOrderCode();

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)order.Total, // PayOS uses integer amounts (VND, no decimals)
            Description = $"XT {order.OrderNumber}",
            ReturnUrl = returnUrl,
            CancelUrl = cancelUrl,
            ExpiredAt = (int)DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds(),
            BuyerName = order.CustomerName,
            BuyerEmail = order.CustomerEmail,
            BuyerPhone = order.CustomerPhone,
            BuyerAddress = $"{order.ShippingAddress}, {order.ShippingWard}, {order.ShippingDistrict}, {order.ShippingCity}",
            // PayOS description limit is 9 chars for non-linked accounts.
            // Keep it short, store orderCode→OrderId mapping via the webhook.
            Items = order.Items.Select(i => new PaymentLinkItem
            {
                Name = TruncateProductName(i.ProductName),
                Quantity = i.Quantity,
                Price = (int)i.UnitPrice
            }).ToList()
        };

        var result = await _client.PaymentRequests.CreateAsync(request);

        _logger.LogInformation(
            "Created PayOS payment link for order {OrderNumber} (code={OrderCode}): {Url}",
            order.OrderNumber, orderCode, result.CheckoutUrl);

        return new PaymentLinkResult(result.CheckoutUrl, orderCode);
    }

    public async Task<WebhookResult> HandleWebhookAsync(
        string webhookBody, string signature, CancellationToken ct = default)
    {
        try
        {
            // Parse the webhook body
            var webhook = System.Text.Json.JsonSerializer.Deserialize<Webhook>(
                webhookBody,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Invalid webhook payload");

            var verified = await _client.Webhooks.VerifyAsync(webhook);

            _logger.LogInformation(
                "PayOS webhook verified: OrderCode={OrderCode}, Code={Code}",
                verified.OrderCode, verified.Code);

            var isSuccess = verified.Code == "00";

            return new WebhookResult(verified.OrderCode, isSuccess, isSuccess ? "PAID" : "FAILED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify PayOS webhook");
            throw;
        }
    }

    /// <summary>
    /// Generate a unique order code for PayOS (positive long, max ~9 digits for safety).
    /// Uses timestamp-based approach: last 6 digits of unix timestamp + 3 random digits.
    /// </summary>
    private static long GenerateOrderCode()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1_000_000;
        var random = Random.Shared.Next(100, 999);
        return timestamp * 1000 + random;
    }

    private static string TruncateProductName(string name)
        => name.Length > 256 ? name[..256] : name;
}
