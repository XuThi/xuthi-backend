using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody);
    Task SendVerificationEmailAsync(string to, string verificationLink);
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

    public async Task SendVerificationEmailAsync(string to, string verificationLink)
    {
        var subject = "Xác nhận địa chỉ email - XuThi Store";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #ffffff; padding: 30px; border: 1px solid #e0e0e0; border-top: none; }}
        .button {{ display: inline-block; background: #667eea; color: white; padding: 14px 30px; text-decoration: none; border-radius: 5px; font-weight: bold; margin: 20px 0; }}
        .button:hover {{ background: #5a6fd6; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .link {{ word-break: break-all; color: #667eea; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🛍️ XuThi Store</h1>
            <p>Xác nhận địa chỉ email của bạn</p>
        </div>
        <div class=""content"">
            <h2>Chào mừng bạn đến với XuThi Store!</h2>
            <p>Cảm ơn bạn đã đăng ký tài khoản. Vui lòng xác nhận địa chỉ email của bạn bằng cách nhấn vào nút bên dưới:</p>
            
            <div style=""text-align: center;"">
                <a href=""{verificationLink}"" class=""button"">Xác nhận Email</a>
            </div>
            
            <p>Hoặc copy và dán link sau vào trình duyệt:</p>
            <p class=""link"">{verificationLink}</p>
            
            <p><strong>Lưu ý:</strong> Link này sẽ hết hạn sau 24 giờ.</p>
            
            <p>Nếu bạn không đăng ký tài khoản này, vui lòng bỏ qua email này.</p>
        </div>
        <div class=""footer"">
            <p>© 2026 XuThi Store. All rights reserved.</p>
            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(to, subject, htmlBody);
    }
}
