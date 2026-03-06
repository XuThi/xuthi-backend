using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Users.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody);
    Task SendPromotionalEmailAsync(string to, string subject, string htmlBody);
    Task SendVerificationEmailAsync(string to, string verificationLink);
    Task SendPasswordResetEmailAsync(string to, string resetLink);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpEmail;
    private readonly string _smtpPassword;
    private readonly string _fromName;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
        _smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        _smtpEmail = _configuration["Email:SmtpEmail"] ?? "";
        _smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
        _fromName = _configuration["Email:FromName"] ?? "XuThi Store";
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrEmpty(_smtpEmail) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogWarning("Email not configured. Skipping email to {To}", to);
            return;
        }

        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_smtpEmail, _smtpPassword)
        };

        var message = new MailMessage
        {
            From = new MailAddress(_smtpEmail, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent successfully to {To}", to);
    }

    public async Task SendPromotionalEmailAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrEmpty(_smtpEmail) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogWarning("Email not configured. Skipping promotional email to {To}", to);
            return;
        }

        using var client = new SmtpClient(_smtpHost, _smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_smtpEmail, _smtpPassword)
        };

        var message = new MailMessage
        {
            From = new MailAddress(_smtpEmail, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(to);

        // Headers to route to Gmail's Promotions tab
        message.Headers.Add("Precedence", "bulk");
        message.Headers.Add("X-Auto-Response-Suppress", "All");
        message.Headers.Add("X-Mailer", "XuThi Store Marketing");
        message.Headers.Add("List-Unsubscribe", $"<mailto:{_smtpEmail}?subject=unsubscribe>");

        await client.SendMailAsync(message);
        _logger.LogInformation("Promotional email sent successfully to {To}", to);
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
