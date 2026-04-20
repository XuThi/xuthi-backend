using Core.Security;
using Microsoft.Extensions.Configuration;

namespace Customer.Customers.Features.UnsubscribeMarketing;

public class UnsubscribeMarketingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/marketing/unsubscribe", async (
            string? token,
            CustomerDbContext db,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            await ApplyUnsubscribeAsync(token, db, configuration, cancellationToken);

            // RFC 8058 one-click requires an empty 200/202 response for POST requests.
            return Results.Text(string.Empty, "text/plain", statusCode: StatusCodes.Status202Accepted);
        })
        .WithName("OneClickUnsubscribeMarketing")
        .WithTags("Marketing")
        .WithSummary("One-click unsubscribe endpoint for marketing emails")
        .WithDescription("Handles RFC 8058 List-Unsubscribe one-click POST requests")
        .DisableAntiforgery();

        app.MapGet("/api/marketing/unsubscribe", async (
            string? token,
            CustomerDbContext db,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var result = await ApplyUnsubscribeAsync(token, db, configuration, cancellationToken);
            var html = BuildUnsubscribePage(result);
            return Results.Content(html, "text/html; charset=utf-8");
        })
        .WithName("GetUnsubscribeMarketingPage")
        .WithTags("Marketing")
        .WithSummary("Unsubscribe confirmation page for marketing emails")
        .WithDescription("Handles standard GET unsubscribe page requests from unsubscribe links");
    }

    private static async Task<UnsubscribeResult> ApplyUnsubscribeAsync(
        string? token,
        CustomerDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var signingKey = configuration["Resend:UnsubscribeSigningKey"]
            ?? configuration["Jwt:Key"]
            ?? string.Empty;

        if (!MarketingUnsubscribeToken.TryValidateToken(token, signingKey, out var email))
            return UnsubscribeResult.InvalidToken;

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.Email, email), cancellationToken);

        // Return success even if customer is not found to avoid exposing user-list details.
        if (customer is null)
            return UnsubscribeResult.Success;

        if (!customer.AcceptsMarketing)
            return UnsubscribeResult.AlreadyUnsubscribed;

        customer.AcceptsMarketing = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return UnsubscribeResult.Success;
    }

    private static string BuildUnsubscribePage(UnsubscribeResult result)
    {
        var title = result switch
        {
            UnsubscribeResult.Success => "Đã hủy đăng ký email marketing",
            UnsubscribeResult.AlreadyUnsubscribed => "Bạn đã hủy đăng ký trước đó",
            _ => "Liên kết hủy đăng ký không hợp lệ"
        };

        var description = result switch
        {
            UnsubscribeResult.Success => "Bạn sẽ không nhận thêm email marketing từ XuThi Store.",
            UnsubscribeResult.AlreadyUnsubscribed => "Bạn hiện không còn trong danh sách email marketing của XuThi Store.",
            _ => "Liên kết có thể đã hết hạn hoặc không hợp lệ. Vui lòng thử lại từ email gần nhất của bạn."
        };

        return $$"""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>{{title}}</title>
        </head>
        <body style="margin:0;padding:0;background:#f3f4f6;font-family:Arial,Helvetica,sans-serif;color:#111827;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:28px 12px;">
                <tr>
                    <td align="center">
                        <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;width:100%;background:#ffffff;border:1px solid #e5e7eb;">
                            <tr>
                                <td style="padding:16px 20px;border-bottom:1px solid #f3f4f6;">
                                    <p style="margin:0;font-size:18px;line-height:24px;font-weight:700;">XuThi Store</p>
                                </td>
                            </tr>
                            <tr>
                                <td style="padding:16px 20px 20px 20px;">
                                    <p style="margin:0;font-size:18px;line-height:24px;font-weight:700;color:#111827;">{{title}}</p>
                                    <p style="margin:10px 0 0 0;font-size:14px;line-height:21px;color:#374151;">{{description}}</p>
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

    private enum UnsubscribeResult
    {
        Success,
        AlreadyUnsubscribed,
        InvalidToken
    }
}