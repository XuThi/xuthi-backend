using Customer.Data;
using Identity.Users.Services;
using Microsoft.Extensions.DependencyInjection;
using ProductCatalog.Products.Events;

namespace Notification.Handlers;

/// <summary>
/// Sends marketing email to all subscribers when a new product is created.
/// Triggered by ProductCreatedEvent domain event via MediatR.
/// </summary>
internal class NewProductNotificationHandler(
    IServiceScopeFactory scopeFactory)
    : INotificationHandler<ProductCreatedEvent>
{
    public Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => SendNotificationBatchAsync(notification), CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task SendNotificationBatchAsync(ProductCreatedEvent notification)
    {
        using var scope = scopeFactory.CreateScope();
        var customerDb = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var subscribers = await customerDb.Customers
            .Where(c => c.AcceptsMarketing && c.Email != null)
            .Select(c => c.Email)
            .ToListAsync();

        var batch = subscribers.Take(50).ToList();
        if (batch.Count == 0)
            return;

        var subject = $"Cập nhật sản phẩm: {notification.ProductName}";
        var htmlBody = BuildNewProductHtml(notification);
        var textBody = BuildNewProductText(notification);

        for (var i = 0; i < batch.Count; i++)
        {
            await emailService.SendPromotionalEmailAsync(batch[i], subject, htmlBody, textBody);

            if (i < batch.Count - 1)
                await Task.Delay(200);
        }
    }

    private static string BuildNewProductHtml(ProductCreatedEvent product)
    {
        var imageSection = !string.IsNullOrEmpty(product.ImageUrl)
            ? $"""
            <tr>
                <td style="padding: 6px 20px 0 20px; text-align: center;">
                    <img src="{product.ImageUrl}" alt="{product.ProductName}" style="width: 100%; max-width: 320px; height: auto; border: 1px solid #e5e7eb; border-radius: 4px;" />
                </td>
            </tr>
            """
            : "";

        var shopLink = product.Slug is not null
            ? $"https://xuthi.com/product/{product.Slug}"
            : "https://xuthi.com";

        var priceSection = product.BasePrice.HasValue
            ? $"""
            <tr>
                <td style="padding: 6px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                    Giá tham khảo từ: <strong style="color: #111827; font-weight: 600;">{product.BasePrice.Value:N0}₫</strong>
                </td>
            </tr>
            """
            : "";

        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
        </head>
        <body style="margin: 0; padding: 0; background-color: #f3f4f6; font-family: Arial, Helvetica, sans-serif; color: #1f2937;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">
                XuThi Store cập nhật sản phẩm mới phù hợp với danh mục bạn đang theo dõi.
            </div>
            <table width="100%" cellpadding="0" cellspacing="0" style="background-color: #f3f4f6; padding: 16px 8px;">
                <tr>
                    <td align="center">
                        <table width="600" cellpadding="0" cellspacing="0" style="max-width: 600px; width: 100%; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 6px; overflow: hidden;">
                            <tr>
                                <td style="padding: 12px 20px; border-bottom: 1px solid #f3f4f6;">
                                    <p style="margin: 0; font-size: 16px; line-height: 22px; font-weight: 700; color: #111827;">XuThi Store</p>
                                    <p style="margin: 1px 0 0 0; font-size: 12px; line-height: 16px; color: #6b7280;">Bản tin cập nhật sản phẩm</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 10px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                                    <p style="margin: 0 0 6px 0;">Xin chào,</p>
                                    <p style="margin: 0;">XuThi Store vừa có sản phẩm mới trong danh mục bạn đã đăng ký nhận tin.</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 6px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                                    <p style="margin: 0;">Sản phẩm mới: {{product.ProductName}}</p>
                                </td>
                            </tr>
                            {{priceSection}}
                            {{imageSection}}
                            <tr>
                                <td style="padding: 6px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                                    <p style="margin: 0;">Bạn có thể xem chi tiết thông tin sản phẩm tại đường dẫn bên dưới.</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 6px 20px 14px 20px;">
                                    <a href="{{shopLink}}" style="display: inline-block; background-color: #111827; color: #ffffff !important; padding: 9px 14px; text-decoration: none; font-weight: 600; font-size: 13px; line-height: 18px; border-radius: 4px;">Xem chi tiết sản phẩm</a>
                                </td>
                            </tr>
                            <tr>
                                <td style="background-color: #f9fafb; border-top: 1px solid #e5e7eb; padding: 10px 20px; color: #6b7280; font-size: 12px; line-height: 17px;">
                                    <p style="margin: 0;">Bạn nhận email này vì đã đăng ký nhận cập nhật sản phẩm từ XuThi Store.</p>
                                    <p style="margin: 6px 0 0 0;">Để hủy đăng ký, dùng tùy chọn <strong>Unsubscribe</strong> trong ứng dụng email hoặc trả lời email này với tiêu đề <strong>unsubscribe</strong>.</p>
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

    private static string BuildNewProductText(ProductCreatedEvent product)
    {
        var shopLink = product.Slug is not null
            ? $"https://xuthi.com/product/{product.Slug}"
            : "https://xuthi.com";

        var priceText = product.BasePrice.HasValue
            ? $"Giá tham khảo từ: {product.BasePrice.Value:N0}₫"
            : "";

        return $"""
        Cập nhật sản phẩm

        Xin chào,

        XuThi Store vừa có sản phẩm mới trong danh mục bạn đã đăng ký nhận tin.

        Sản phẩm mới: {product.ProductName}
        {priceText}

        Xem chi tiết tại: {shopLink}

        Bạn nhận email này vì đã đăng ký nhận cập nhật sản phẩm từ XuThi Store.
        Để hủy đăng ký, dùng tùy chọn Unsubscribe trong ứng dụng email hoặc trả lời email này với tiêu đề: unsubscribe.
        """;
    }
}
