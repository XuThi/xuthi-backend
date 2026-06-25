using Microsoft.Extensions.Configuration;
using Order.Orders.OrderIntake;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using System.Text.Json;

namespace Order.Orders.Services;

public class PayOsPaymentService : IPaymentService
{
    private readonly PayOSClient _client;

    public PayOsPaymentService(IConfiguration configuration)
    {
        var clientId = configuration["PayOS:ClientId"];
        var apiKey = configuration["PayOS:ApiKey"];
        var checksumKey = configuration["PayOS:ChecksumKey"];

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("PayOS:ClientId is not configured");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("PayOS:ApiKey is not configured");
        if (string.IsNullOrWhiteSpace(checksumKey))
            throw new InvalidOperationException("PayOS:ChecksumKey is not configured");

        _client = new PayOSClient(clientId, apiKey, checksumKey);
    }

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(
        CustomerOrder order,
        string returnUrl,
        string cancelUrl,
        DateTimeOffset expiresAt,
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
            ExpiredAt = (int)expiresAt.ToUnixTimeSeconds(),
            BuyerName = order.CustomerName,
            BuyerEmail = order.CustomerEmail,
            BuyerPhone = order.CustomerPhone,
            BuyerAddress = $"{order.ShippingAddress}, {order.ShippingWard}, {order.ShippingCity}",
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

        return new PaymentLinkResult(result.CheckoutUrl, orderCode, result.PaymentLinkId);
    }

    public async Task CancelPaymentLinkAsync(
        long orderCode,
        string reason,
        CancellationToken ct = default)
    {
        await _client.PaymentRequests.CancelAsync(orderCode, reason);
    }

    public async Task<PayOsPaymentResult> VerifyWebhookAsync(string rawPayload, CancellationToken ct = default)
    {
        var webhookPayload = JsonSerializer.Deserialize<Webhook>(
            rawPayload,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new JsonException("Invalid PayOS webhook payload");

        if (webhookPayload?.Data is null || string.IsNullOrWhiteSpace(webhookPayload.Signature))
        {
            throw new InvalidOperationException("Invalid webhook payload");
        }

        var verified = await _client.Webhooks.VerifyAsync(webhookPayload);

        return new PayOsPaymentResult(
            verified.OrderCode,
            MapPaymentStatus(verified.Code),
            verified.Code);
    }

    private static PayOsPaymentResultStatus MapPaymentStatus(string? providerStatus)
    {
        return providerStatus?.Trim().ToUpperInvariant() switch
        {
            "00" or "PAID" or "SUCCESS" => PayOsPaymentResultStatus.Paid,
            "CANCELLED" or "CANCELED" or "CANCEL" => PayOsPaymentResultStatus.Cancelled,
            "PENDING" => PayOsPaymentResultStatus.Pending,
            "PROCESSING" => PayOsPaymentResultStatus.Processing,
            _ => PayOsPaymentResultStatus.Failed
        };
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
