using Identity.Users.Services;
using Order.Orders.Events;

namespace Notification.Handlers;

/// <summary>
/// Sends order confirmation email when an order is created.
/// Triggered by OrderCreatedEvent domain event via MediatR.
/// </summary>
internal class OrderCreatedEmailHandler(
    IEmailService emailService,
    ILogger<OrderCreatedEmailHandler> logger)
    : INotificationHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Sending order confirmation email for order {OrderNumber} to {Email}",
            notification.OrderNumber, notification.CustomerEmail);

        var subject = $"Xác nhận đơn hàng #{notification.OrderNumber} - XuThi Store";
        var htmlBody = BuildOrderConfirmationHtml(notification);

        try
        {
            await emailService.SendEmailAsync(notification.CustomerEmail, subject, htmlBody);
            logger.LogInformation("Order confirmation email sent for {OrderNumber}", notification.OrderNumber);
        }
        catch (Exception ex)
        {
            // Don't fail the order flow if email fails
            logger.LogError(ex, "Failed to send order confirmation email for {OrderNumber}", notification.OrderNumber);
        }
    }

    private static string BuildOrderConfirmationHtml(OrderCreatedEvent order)
    {
        var itemRows = string.Join("", order.Items.Select(item =>
            $"""<tr><td style="padding: 12px 0; border-bottom: 1px solid #eee; font-size: 14px;">{item.ProductName}<br/><span style="color: #888; font-size: 13px;">{item.VariantDescription}</span></td><td style="padding: 12px 8px; border-bottom: 1px solid #eee; text-align: center; font-size: 14px;">{item.Quantity}</td><td style="padding: 12px 0; border-bottom: 1px solid #eee; text-align: right; font-size: 14px; white-space: nowrap;">{item.TotalPrice:N0}₫</td></tr>"""));

        var discountRow = order.DiscountAmount > 0
            ? $"""<tr><td colspan="2" style="padding: 4px 0; text-align: right; font-size: 14px; color: #555;">Giảm giá:</td><td style="padding: 4px 0; text-align: right; font-size: 14px; color: #555;">-{order.DiscountAmount:N0}₫</td></tr>"""
            : "";

        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
        </head>
        <body style="margin: 0; padding: 0; background-color: #f5f5f5; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333;">
            <table width="100%" cellpadding="0" cellspacing="0" style="background-color: #f5f5f5; padding: 40px 20px;">
                <tr>
                    <td align="center">
                        <table width="600" cellpadding="0" cellspacing="0" style="max-width: 600px; width: 100%;">
                            <!-- Header -->
                            <tr>
                                <td style="background-color: #000000; padding: 32px 40px; text-align: center;">
                                    <h1 style="margin: 0; font-size: 28px; font-weight: 700; color: #ffffff; letter-spacing: 2px;">XUTHI STORE</h1>
                                </td>
                            </tr>
                            <!-- Content -->
                            <tr>
                                <td style="background-color: #ffffff; padding: 40px;">
                                    <h2 style="margin: 0 0 16px 0; font-size: 22px; color: #111; font-weight: 600;">Đặt hàng thành công!</h2>
                                    <p style="margin: 0 0 8px 0; color: #444; font-size: 15px;">Xin chào <strong>{{order.CustomerName}}</strong>,</p>
                                    <p style="margin: 0 0 24px 0; color: #444; font-size: 15px;">Cảm ơn bạn đã đặt hàng tại XuThi Store. Đơn hàng của bạn đã được tiếp nhận.</p>
                                    
                                    <!-- Order Info -->
                                    <table width="100%" cellpadding="0" cellspacing="0" style="background-color: #f5f5f5; margin: 0 0 24px 0;">
                                        <tr>
                                            <td style="padding: 16px 20px; font-size: 14px; color: #333;">
                                                <strong>Mã đơn hàng:</strong> {{order.OrderNumber}}<br/>
                                                <strong>Người nhận:</strong> {{order.CustomerName}}<br/>
                                                <strong>SĐT:</strong> {{order.CustomerPhone}}<br/>
                                                <strong>Địa chỉ:</strong> {{order.ShippingAddress}}, {{order.ShippingWard}}, {{order.ShippingDistrict}}, {{order.ShippingCity}}
                                            </td>
                                        </tr>
                                    </table>

                                    <!-- Items Table -->
                                    <table width="100%" cellpadding="0" cellspacing="0" style="border-collapse: collapse;">
                                        <thead>
                                            <tr>
                                                <th style="text-align: left; padding: 10px 0; border-bottom: 2px solid #111; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; color: #111;">Sản phẩm</th>
                                                <th style="text-align: center; padding: 10px 8px; border-bottom: 2px solid #111; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; color: #111;">SL</th>
                                                <th style="text-align: right; padding: 10px 0; border-bottom: 2px solid #111; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; color: #111;">Thành tiền</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {{itemRows}}
                                        </tbody>
                                    </table>

                                    <!-- Totals -->
                                    <table width="100%" cellpadding="0" cellspacing="0" style="margin-top: 16px;">
                                        <tr>
                                            <td colspan="2" style="padding: 4px 0; text-align: right; font-size: 14px; color: #555;">Tạm tính:</td>
                                            <td style="padding: 4px 0; text-align: right; font-size: 14px; color: #555; width: 120px;">{{order.Subtotal:N0}}₫</td>
                                        </tr>
                                        {{discountRow}}
                                        <tr>
                                            <td colspan="2" style="padding: 4px 0; text-align: right; font-size: 14px; color: #555;">Phí vận chuyển:</td>
                                            <td style="padding: 4px 0; text-align: right; font-size: 14px; color: #555;">{{order.ShippingFee:N0}}₫</td>
                                        </tr>
                                        <tr>
                                            <td colspan="2" style="padding: 8px 0 0 0; text-align: right; font-size: 18px; font-weight: 700; color: #000; border-top: 2px solid #111;">Tổng cộng:</td>
                                            <td style="padding: 8px 0 0 0; text-align: right; font-size: 18px; font-weight: 700; color: #000; border-top: 2px solid #111;">{{order.Total:N0}}₫</td>
                                        </tr>
                                    </table>

                                    <p style="margin: 24px 0 0 0; color: #888; font-size: 13px;">Chúng tôi sẽ liên hệ với bạn khi đơn hàng được xử lý.</p>
                                </td>
                            </tr>
                            <!-- Footer -->
                            <tr>
                                <td style="background-color: #fafafa; border-top: 1px solid #eee; padding: 24px 40px; text-align: center;">
                                    <p style="margin: 0; color: #999; font-size: 12px;">XuThi Store</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>
        """;
    }
}
