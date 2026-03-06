using Customer.Data;
using Identity.Users.Services;
using Promotion.SaleCampaigns.Events;

namespace Notification.Handlers;

/// <summary>
/// Sends marketing email to all subscribers when a new sale campaign is created.
/// Triggered by SaleCampaignCreatedEvent domain event via MediatR.
/// </summary>
internal class NewSaleCampaignNotificationHandler(
    CustomerDbContext customerDb,
    IEmailService emailService,
    ILogger<NewSaleCampaignNotificationHandler> logger)
    : INotificationHandler<SaleCampaignCreatedEvent>
{
    public async Task Handle(SaleCampaignCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "New sale campaign created: {CampaignName} ({CampaignId}). Notifying subscribers...",
            notification.CampaignName, notification.CampaignId);

        try
        {
            // Get all customers who subscribed to marketing emails
            var subscribers = await customerDb.Customers
                .Where(c => c.AcceptsMarketing && c.Email != null)
                .Select(c => c.Email)
                .ToListAsync(cancellationToken);

            if (subscribers.Count == 0)
            {
                logger.LogInformation("No subscribers to notify for new sale campaign.");
                return;
            }

            var subject = $"Khuyến mãi mới: {notification.CampaignName} - XuThi Store";
            var htmlBody = BuildSaleCampaignHtml(notification);

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
                    logger.LogWarning(ex, "Failed to send sale campaign email to {Email}", email);
                }
            }

            logger.LogInformation(
                "Sale campaign notification sent to {SentCount}/{TotalCount} subscribers",
                sent, subscribers.Count);
        }
        catch (Exception ex)
        {
            // Don't fail the campaign creation flow
            logger.LogError(ex, "Failed to send sale campaign notifications for {CampaignId}", notification.CampaignId);
        }
    }

    private static string BuildSaleCampaignHtml(SaleCampaignCreatedEvent campaign)
    {
        var bannerSection = !string.IsNullOrEmpty(campaign.BannerImageUrl)
            ? $"""<tr><td style="padding: 0;"><img src="{campaign.BannerImageUrl}" alt="{campaign.CampaignName}" style="width: 100%; max-width: 600px; height: auto; display: block;" /></td></tr>"""
            : "";

        var campaignLink = campaign.Slug is not null
            ? $"https://xuthi.store/sale/{campaign.Slug}"
            : "https://xuthi.store";

        var typeLabel = campaign.Type switch
        {
            Promotion.SaleCampaigns.Models.SaleCampaignType.FlashSale => "FLASH SALE",
            Promotion.SaleCampaigns.Models.SaleCampaignType.SeasonalSale => "KHUYẾN MÃI THEO MÙA",
            Promotion.SaleCampaigns.Models.SaleCampaignType.Clearance => "XẢ HÀNG",
            Promotion.SaleCampaigns.Models.SaleCampaignType.MemberExclusive => "ƯU ĐÃI THÀNH VIÊN",
            _ => "KHUYẾN MÃI"
        };

        var dateRange = $"{campaign.StartDate:dd/MM/yyyy} — {campaign.EndDate:dd/MM/yyyy}";

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
                            <!-- Banner Image -->
                            {{bannerSection}}
                            <!-- Content -->
                            <tr>
                                <td style="background-color: #ffffff; padding: 40px;">
                                    <p style="margin: 0 0 4px 0; font-size: 12px; text-transform: uppercase; letter-spacing: 2px; color: #999; font-weight: 600;">{{typeLabel}}</p>
                                    <h2 style="margin: 0 0 20px 0; font-size: 24px; color: #111; font-weight: 700;">{{campaign.CampaignName}}</h2>
                                    
                                    <table width="100%" cellpadding="0" cellspacing="0" style="background-color: #f5f5f5; margin: 0 0 24px 0;">
                                        <tr>
                                            <td style="padding: 16px 20px;">
                                                <p style="margin: 0 0 4px 0; color: #555; font-size: 14px;">Thời gian: <strong style="color: #111;">{{dateRange}}</strong></p>
                                                <p style="margin: 0; color: #555; font-size: 14px;"><strong style="color: #111;">{{campaign.ItemCount}}</strong> sản phẩm được giảm giá</p>
                                            </td>
                                        </tr>
                                    </table>

                                    <p style="margin: 0 0 24px 0; color: #555; font-size: 15px;">Đừng bỏ lỡ cơ hội mua sắm với giá ưu đãi tại XuThi Store!</p>
                                    <div style="text-align: center;">
                                        <a href="{{campaignLink}}" style="display: inline-block; background-color: #000000; color: #ffffff !important; padding: 14px 36px; text-decoration: none; font-weight: 600; font-size: 15px; letter-spacing: 0.5px;">Mua sắm ngay</a>
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
