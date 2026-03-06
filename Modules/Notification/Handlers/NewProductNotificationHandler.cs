using Customer.Data;
using Identity.Users.Services;
using ProductCatalog.Products.Events;

namespace Notification.Handlers;

/// <summary>
/// Sends marketing email to all subscribers when a new product is created.
/// Triggered by ProductCreatedEvent domain event via MediatR.
/// </summary>
internal class NewProductNotificationHandler(
    CustomerDbContext customerDb,
    IEmailService emailService,
    ILogger<NewProductNotificationHandler> logger)
    : INotificationHandler<ProductCreatedEvent>
{
    public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "New product created: {ProductName} ({ProductId}). Notifying subscribers...",
            notification.ProductName, notification.ProductId);

        try
        {
            // Get all customers who subscribed to marketing emails
            var subscribers = await customerDb.Customers
                .Where(c => c.AcceptsMarketing && c.Email != null)
                .Select(c => c.Email)
                .ToListAsync(cancellationToken);

            if (subscribers.Count == 0)
            {
                logger.LogInformation("No subscribers to notify for new product.");
                return;
            }

            var subject = $"Sản phẩm mới: {notification.ProductName} - XuThi Store";
            var htmlBody = BuildNewProductHtml(notification);

            var sent = 0;
            foreach (var email in subscribers)
            {
                try
                {
                    await emailService.SendPromotionalEmailAsync(email, subject, htmlBody);
                    sent++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to send new product email to {Email}", email);
                }
            }

            logger.LogInformation(
                "New product notification sent to {SentCount}/{TotalCount} subscribers",
                sent, subscribers.Count);
        }
        catch (Exception ex)
        {
            // Don't fail the product creation flow
            logger.LogError(ex, "Failed to send new product notifications for {ProductId}", notification.ProductId);
        }
    }

    private static string BuildNewProductHtml(ProductCreatedEvent product)
    {
        var imageSection = !string.IsNullOrEmpty(product.ImageUrl)
            ? $"""<tr><td style="padding: 0;"><img src="{product.ImageUrl}" alt="{product.ProductName}" style="width: 100%; max-width: 600px; height: auto; display: block;" /></td></tr>"""
            : "";

        var shopLink = product.Slug is not null
            ? $"https://xuthi.store/product/{product.Slug}"
            : "https://xuthi.store";

        var priceSection = product.BasePrice.HasValue
            ? $"""<p style="margin: 0 0 16px 0; font-size: 22px; font-weight: 700; color: #000;">Từ {product.BasePrice.Value:N0}₫</p>"""
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
                            <!-- Product Image -->
                            {{imageSection}}
                            <!-- Content -->
                            <tr>
                                <td style="background-color: #ffffff; padding: 40px;">
                                    <p style="margin: 0 0 8px 0; font-size: 12px; text-transform: uppercase; letter-spacing: 2px; color: #999; font-weight: 600;">Sản phẩm mới</p>
                                    <h2 style="margin: 0 0 16px 0; font-size: 24px; color: #111; font-weight: 700;">{{product.ProductName}}</h2>
                                    {{priceSection}}
                                    <p style="margin: 0 0 24px 0; color: #555; font-size: 15px;">Chúng tôi vừa ra mắt sản phẩm mới tại XuThi Store. Hãy khám phá ngay!</p>
                                    <div style="text-align: center;">
                                        <a href="{{shopLink}}" style="display: inline-block; background-color: #000000; color: #ffffff !important; padding: 14px 36px; text-decoration: none; font-weight: 600; font-size: 15px; letter-spacing: 0.5px;">Xem sản phẩm</a>
                                    </div>
                                </td>
                            </tr>
                            <!-- Footer -->
                            <tr>
                                <td style="background-color: #fafafa; border-top: 1px solid #eee; padding: 24px 40px; text-align: center;">
                                    <p style="margin: 0 0 4px 0; color: #999; font-size: 12px;">XuThi Store</p>
                                    <p style="margin: 0; color: #999; font-size: 12px;">Bạn nhận email này vì đã đăng ký nhận thông tin khuyến mãi.</p>
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
