using Customer.Data;
using Identity.Users.Services;
using Microsoft.Extensions.DependencyInjection;
using Promotion.SaleCampaigns.Events;
using Promotion.SaleCampaigns.Models;

namespace Notification.Handlers;

/// <summary>
/// Sends marketing email to all subscribers when a new sale campaign is created.
/// Triggered by SaleCampaignCreatedEvent domain event via MediatR.
/// </summary>
internal class NewSaleCampaignNotificationHandler(
    IServiceScopeFactory scopeFactory)
    : INotificationHandler<SaleCampaignCreatedEvent>
{
    public Task Handle(SaleCampaignCreatedEvent notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(() => SendNotificationBatchAsync(notification), CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task SendNotificationBatchAsync(SaleCampaignCreatedEvent notification)
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

        var subject = $"Cập nhật chương trình giảm giá: {notification.CampaignName}";
        var htmlBody = BuildSaleCampaignHtml(notification);
        var textBody = BuildSaleCampaignText(notification);

        for (var i = 0; i < batch.Count; i++)
        {
            await emailService.SendPromotionalEmailAsync(batch[i], subject, htmlBody, textBody);

            if (i < batch.Count - 1)
                await Task.Delay(200);
        }
    }

    private static string BuildSaleCampaignHtml(SaleCampaignCreatedEvent campaign)
    {
        var bannerSection = !string.IsNullOrEmpty(campaign.BannerImageUrl)
            ? $"""
            <tr>
                <td style="padding: 6px 20px 0 20px; text-align: center;">
                    <img src="{campaign.BannerImageUrl}" alt="{campaign.CampaignName}" style="width: 100%; max-width: 360px; height: auto; border: 1px solid #e5e7eb; border-radius: 4px;" />
                </td>
            </tr>
            """
            : "";

        var campaignLink = campaign.Slug is not null
            ? $"https://xuthi.com/sale/{campaign.Slug}"
            : "https://xuthi.com";

        var typeLabel = campaign.Type switch
        {
            SaleCampaignType.FlashSale => "FLASH SALE",
            SaleCampaignType.SeasonalSale => "KHUYẾN MÃI THEO MÙA",
            SaleCampaignType.Clearance => "XẢ HÀNG",
            SaleCampaignType.MemberExclusive => "ƯU ĐÃI THÀNH VIÊN",
            _ => "KHUYẾN MÃI"
        };

        var dateRange = $"{campaign.StartDate:dd/MM/yyyy} - {campaign.EndDate:dd/MM/yyyy}";

        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
        </head>
        <body style="margin: 0; padding: 0; background-color: #f3f4f6; font-family: Arial, Helvetica, sans-serif; color: #1f2937;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">
                XuThi Store cập nhật chương trình giảm giá mới cho khách hàng đã đăng ký nhận tin.
            </div>
            <table width="100%" cellpadding="0" cellspacing="0" style="background-color: #f3f4f6; padding: 16px 8px;">
                <tr>
                    <td align="center">
                        <table width="600" cellpadding="0" cellspacing="0" style="max-width: 600px; width: 100%; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 6px; overflow: hidden;">
                            <tr>
                                <td style="padding: 12px 20px; border-bottom: 1px solid #f3f4f6;">
                                    <p style="margin: 0; font-size: 16px; line-height: 22px; font-weight: 700; color: #111827;">XuThi Store</p>
                                    <p style="margin: 1px 0 0 0; font-size: 12px; line-height: 16px; color: #6b7280;">Bản tin cập nhật khuyến mãi</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 10px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                                    <p style="margin: 0 0 6px 0;">Xin chào,</p>
                                    <p style="margin: 0;">Đây là email cập nhật chương trình giảm giá mới dành cho khách hàng đã đăng ký nhận tin từ XuThi Store.</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 6px 20px 0 20px;">
                                    <h2 style="margin: 0; font-size: 18px; line-height: 24px; color: #111827; font-weight: 600;">{{campaign.CampaignName}}</h2>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 6px 20px 0 20px; color: #6b7280; font-size: 12px; line-height: 17px; text-transform: uppercase; letter-spacing: 0.8px;">
                                    {{typeLabel}}
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 6px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                                    Thời gian áp dụng: <strong style="color: #111827; font-weight: 600;">{{dateRange}}</strong>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 6px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                                    Số lượng sản phẩm tham gia: <strong style="color: #111827; font-weight: 600;">{{campaign.ItemCount}}</strong>
                                </td>
                            </tr>
                            {{bannerSection}}
                            <tr>
                                <td style="padding: 6px 20px 0 20px; color: #374151; font-size: 14px; line-height: 20px;">
                                    <p style="margin: 0;">Bạn có thể xem chi tiết chương trình tại đường dẫn bên dưới.</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding: 6px 20px 14px 20px;">
                                    <a href="{{campaignLink}}" style="display: inline-block; background-color: #111827; color: #ffffff !important; padding: 9px 14px; text-decoration: none; font-weight: 600; font-size: 13px; line-height: 18px; border-radius: 4px;">Xem chi tiết chương trình</a>
                                </td>
                            </tr>
                            <tr>
                                <td style="background-color: #f9fafb; border-top: 1px solid #e5e7eb; padding: 10px 20px; color: #6b7280; font-size: 12px; line-height: 17px;">
                                    <p style="margin: 0;">Bạn nhận email này vì đã đăng ký nhận email marketing từ XuThi Store.</p>
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

    private static string BuildSaleCampaignText(SaleCampaignCreatedEvent campaign)
    {
        var campaignLink = campaign.Slug is not null
            ? $"https://xuthi.com/sale/{campaign.Slug}"
            : "https://xuthi.com";

        var typeLabel = campaign.Type switch
        {
            SaleCampaignType.FlashSale => "Flash Sale",
            SaleCampaignType.SeasonalSale => "Khuyen mai theo mua",
            SaleCampaignType.Clearance => "Xa hang",
            SaleCampaignType.MemberExclusive => "Uu dai thanh vien",
            _ => "Khuyen mai"
        };

        var dateRange = $"{campaign.StartDate:dd/MM/yyyy} - {campaign.EndDate:dd/MM/yyyy}";

        return $"""
        Cập nhật chương trình giảm giá

        Xin chào,

        XuThi Store vừa cập nhật chương trình giảm giá mới cho khách hàng đã đăng ký nhận tin.

        Tên chương trình: {campaign.CampaignName}
        Loại chương trình: {typeLabel}
        Thời gian áp dụng: {dateRange}
        Số lượng sản phẩm tham gia: {campaign.ItemCount}

        Xem chi tiết tại: {campaignLink}

        Bạn nhận email này vì đã đăng ký nhận email marketing từ XuThi Store.
        Để hủy đăng ký, dùng tùy chọn Unsubscribe trong ứng dụng email hoặc trả lời email này với tiêu đề: unsubscribe.
        """;
    }
}
