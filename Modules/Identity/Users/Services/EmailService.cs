using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Core.Security;

namespace Identity.Users.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody);
    Task SendPromotionalEmailAsync(string to, string subject, string htmlBody, string? textBody = null);
    Task SendOrderEmailAsync(string to, string subject, string htmlBody);
    Task SendOwnerOrderAlertAsync(string subject, string htmlBody);
    Task SendVerificationEmailAsync(string to, string verificationLink);
    Task SendPasswordResetEmailAsync(string to, string resetLink);
}

/// <summary>
/// Email service implementation using the Resend HTTP API.
/// No external NuGet required — just a simple POST to https://api.resend.com/emails.
/// </summary>
public class EmailService(HttpClient httpClient, IConfiguration configuration, ILogger<EmailService> logger) : IEmailService
{
    private readonly string _apiKey = configuration["Resend:ApiKey"];
    private readonly string _fromName = configuration["Resend:FromName"];
    private readonly string _authFromEmail = configuration["Resend:AuthFromEmail"];
    private readonly string _marketingFromEmail = configuration["Resend:MarketingFromEmail"];
    private readonly string _ordersFromEmail = configuration["Resend:OrdersFromEmail"];
    private readonly string _unsubscribeBaseUrl = configuration["Resend:UnsubscribeBaseUrl"];
    private readonly string _unsubscribeSigningKey = configuration["Resend:UnsubscribeSigningKey"] ?? configuration["Jwt:Key"];

    private readonly string _replyToEmail = configuration["Resend:ReplyToEmail"];

    private readonly string _smtpHost = configuration["Email:SmtpHost"];
    private readonly int _smtpPort = Int32.Parse(configuration["Email:SmtpPort"]);
    private readonly string _smtpEmail = configuration["Email:SmtpEmail"];
    private readonly string _smtpPassword = configuration["Email:SmtpPassword"];
    private readonly string _smtpFromName = configuration["Email:FromName"];

    private async Task SendViaResendAsync(
        string fromEmail,
        string to,
        string subject,
        string htmlBody,
        Dictionary<string, string>? extraHeaders = null,
        string? textBody = null)
    {
        if (string.IsNullOrEmpty(_apiKey) || _apiKey == "re_YOUR_RESEND_API_KEY_HERE")
        {
            logger.LogWarning("Resend API key not configured. Skipping email to {To}", to);
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["from"] = $"{_fromName} <{fromEmail}>",
            ["to"] = new[] { to },
            ["subject"] = subject,
            ["html"] = htmlBody
        };

        if (!string.IsNullOrWhiteSpace(textBody))
        {
            payload["text"] = textBody;
        }

        if (!string.IsNullOrWhiteSpace(_replyToEmail))
        {
            payload["reply_to"] = _replyToEmail;
        }

        if (extraHeaders is { Count: > 0 })
        {
            payload["headers"] = extraHeaders;
        }

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

        try
        {
            var response = await httpClient.PostAsync("https://api.resend.com/emails", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Email sent via Resend from {From} to {To}", fromEmail, to);
            }
            else
            {
                logger.LogError("Resend API error {Status}: {Body}", response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email via Resend to {To}", to);
        }
    }

    /// <summary>Auth emails (verification, password reset) — from noreply@</summary>
    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        await SendViaResendAsync(_authFromEmail, to, subject, htmlBody);
    }

    /// <summary>Marketing emails (new products, sale campaigns) — from marketing@</summary>
    public async Task SendPromotionalEmailAsync(string to, string subject, string htmlBody, string? textBody = null)
    {
        var unsubscribeUrl = BuildUnsubscribeUrl(to);

        var headers = new Dictionary<string, string>
        {
            ["Precedence"] = "bulk",
            ["X-Auto-Response-Suppress"] = "All",
            ["X-Mailer"] = "XuThi Store Marketing",
            ["List-Id"] = "XuThi Store Marketing <marketing.xuthi.store>",
            ["List-Unsubscribe"] = $"<{unsubscribeUrl}>, <mailto:{_marketingFromEmail}?subject=unsubscribe>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
            ["Feedback-ID"] = "xuthi:marketing:newsletter"
        };
        await SendViaResendAsync(_marketingFromEmail, to, subject, htmlBody, headers, textBody);
    }

    private string BuildUnsubscribeUrl(string recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(_unsubscribeSigningKey))
            return _unsubscribeBaseUrl;

        var token = MarketingUnsubscribeToken.CreateToken(
            recipientEmail,
            _unsubscribeSigningKey,
            DateTimeOffset.UtcNow.AddDays(365));

        var separator = _unsubscribeBaseUrl.Contains('?') ? '&' : '?';
        return $"{_unsubscribeBaseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }

    /// <summary>Order emails (confirmation, status updates) — from orders@</summary>
    public async Task SendOrderEmailAsync(string to, string subject, string htmlBody)
    {
        await SendViaResendAsync(_ordersFromEmail, to, subject, htmlBody);
    }

    public async Task SendOwnerOrderAlertAsync(string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_smtpHost)
            || string.IsNullOrWhiteSpace(_smtpEmail)
            || string.IsNullOrWhiteSpace(_smtpPassword))
        {
            logger.LogWarning("SMTP configuration is missing for owner alert email.");
            return;
        }

        await SendViaSmtpAsync(_smtpEmail, subject, htmlBody);
    }

    private async Task SendViaSmtpAsync(string to, string subject, string htmlBody)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_smtpEmail, _smtpFromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };

        message.To.Add(new MailAddress(to));

        using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_smtpEmail, _smtpPassword)
        };

        await smtpClient.SendMailAsync(message);
    }

    public async Task SendPasswordResetEmailAsync(string to, string resetLink)
    {
        var subject = "Đặt lại mật khẩu - XuThi Store";
        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            </head>
            <body style=""margin: 0; padding: 0; background-color: #f5f5f5; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333;"">
                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f5f5f5; padding: 40px 20px;"">
                    <tr>
                        <td align=""center"">
                            <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; width: 100%;"">
                                <!-- Header -->
                                <tr>
                                    <td style=""background-color: #000000; padding: 32px 40px; text-align: center;"">
                                        <h1 style=""margin: 0; font-size: 28px; font-weight: 700; color: #ffffff; letter-spacing: 2px;"">XUTHI STORE</h1>
                                    </td>
                                </tr>
                                <!-- Content -->
                                <tr>
                                    <td style=""background-color: #ffffff; padding: 40px;"">
                                        <h2 style=""margin: 0 0 16px 0; font-size: 22px; color: #111; font-weight: 600;"">Đặt lại mật khẩu</h2>
                                        <p style=""margin: 0 0 16px 0; color: #444; font-size: 15px;"">Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhấn vào nút bên dưới để tạo mật khẩu mới:</p>
                            
                                        <div style=""text-align: center; margin: 32px 0;"">
                                            <a href=""{resetLink}"" style=""display: inline-block; background-color: #000000; color: #ffffff !important; padding: 14px 36px; text-decoration: none; font-weight: 600; font-size: 15px; letter-spacing: 0.5px;"">Đặt lại mật khẩu</a>
                                        </div>
                            
                                        <p style=""margin: 0 0 8px 0; color: #666; font-size: 13px;"">Hoặc copy và dán link sau vào trình duyệt:</p>
                                        <p style=""word-break: break-all; color: #000; font-size: 13px; margin: 0 0 24px 0;"">{resetLink}</p>
                            
                                        <div style=""border-top: 1px solid #eee; padding-top: 16px; margin-top: 16px;"">
                                            <p style=""margin: 0 0 8px 0; color: #666; font-size: 13px;""><strong>Lưu ý:</strong> Link này sẽ hết hạn sau 24 giờ.</p>
                                            <p style=""margin: 0; color: #666; font-size: 13px;"">Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. Mật khẩu của bạn sẽ không thay đổi.</p>
                                        </div>
                                    </td>
                                </tr>
                                <!-- Footer -->
                                <tr>
                                    <td style=""background-color: #fafafa; border-top: 1px solid #eee; padding: 24px 40px; text-align: center;"">
                                        <p style=""margin: 0 0 4px 0; color: #999; font-size: 12px;"">© 2025 XuThi Store. All rights reserved.</p>
                                        <p style=""margin: 0; color: #999; font-size: 12px;"">Email này được gửi tự động, vui lòng không trả lời.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";

        await SendEmailAsync(to, subject, htmlBody);
    }

    public async Task SendVerificationEmailAsync(string to, string verificationLink)
    {
        var subject = "Xác nhận địa chỉ email - XuThi Store";
        var htmlBody = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            </head>
            <body style=""margin: 0; padding: 0; background-color: #f5f5f5; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333;"">
                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f5f5f5; padding: 40px 20px;"">
                    <tr>
                        <td align=""center"">
                            <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; width: 100%;"">
                                <!-- Header -->
                                <tr>
                                    <td style=""background-color: #000000; padding: 32px 40px; text-align: center;"">
                                        <h1 style=""margin: 0; font-size: 28px; font-weight: 700; color: #ffffff; letter-spacing: 2px;"">XUTHI STORE</h1>
                                    </td>
                                </tr>
                                <!-- Content -->
                                <tr>
                                    <td style=""background-color: #ffffff; padding: 40px;"">
                                        <h2 style=""margin: 0 0 16px 0; font-size: 22px; color: #111; font-weight: 600;"">Chào mừng bạn!</h2>
                                        <p style=""margin: 0 0 16px 0; color: #444; font-size: 15px;"">Cảm ơn bạn đã đăng ký tài khoản tại XuThi Store. Vui lòng xác nhận địa chỉ email của bạn bằng cách nhấn vào nút bên dưới:</p>
                            
                                        <div style=""text-align: center; margin: 32px 0;"">
                                            <a href=""{verificationLink}"" style=""display: inline-block; background-color: #000000; color: #ffffff !important; padding: 14px 36px; text-decoration: none; font-weight: 600; font-size: 15px; letter-spacing: 0.5px;"">Xác nhận Email</a>
                                        </div>
                            
                                        <p style=""margin: 0 0 8px 0; color: #666; font-size: 13px;"">Hoặc copy và dán link sau vào trình duyệt:</p>
                                        <p style=""word-break: break-all; color: #000; font-size: 13px; margin: 0 0 24px 0;"">{verificationLink}</p>
                            
                                        <div style=""border-top: 1px solid #eee; padding-top: 16px; margin-top: 16px;"">
                                            <p style=""margin: 0 0 8px 0; color: #666; font-size: 13px;""><strong>Lưu ý:</strong> Link này sẽ hết hạn sau 24 giờ.</p>
                                            <p style=""margin: 0; color: #666; font-size: 13px;"">Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
                                        </div>
                                    </td>
                                </tr>
                                <!-- Footer -->
                                <tr>
                                    <td style=""background-color: #fafafa; border-top: 1px solid #eee; padding: 24px 40px; text-align: center;"">
                                        <p style=""margin: 0 0 4px 0; color: #999; font-size: 12px;"">© 2025 XuThi Store. All rights reserved.</p>
                                        <p style=""margin: 0; color: #999; font-size: 12px;"">Email này được gửi tự động, vui lòng không trả lời.</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";

        await SendEmailAsync(to, subject, htmlBody);
    }
}
