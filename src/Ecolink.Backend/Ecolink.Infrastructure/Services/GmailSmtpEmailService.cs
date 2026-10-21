using Ecolink.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Dịch vụ gửi email thông báo và mã xác thực qua giao thức SMTP của Google (Gmail)
/// </summary>
public class GmailSmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailSmtpEmailService> _logger;

    public GmailSmtpEmailService(IConfiguration configuration, ILogger<GmailSmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetToken, CancellationToken ct = default)
    {
        var senderName = _configuration["MailSettings:SenderName"] ?? "EcoLink Support Team";
        var senderEmail = _configuration["MailSettings:SenderEmail"] ?? "no-reply@ecolink.vn";
        var resetUrl = $"https://ecolink.vn/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(recipientEmail)}";

        var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                <h2 style='color: #2e7d32; text-align: center;'>EcoLink - Nền Tảng Thu Gom Rác Thông Minh</h2>
                <p>Xin chào <strong>{recipientName}</strong>,</p>
                <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản liên kết với email này.</p>
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{resetUrl}' style='background-color: #2e7d32; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;'>Đặt Lại Mật Khẩu</a>
                </div>
                <p style='color: #666; font-size: 13px;'>Mã xác nhận bảo mật: <code style='font-weight: bold; background: #f4f4f4; padding: 2px 6px; border-radius: 4px;'>{resetToken}</code></p>
                <p style='color: #666; font-size: 13px;'>Liên kết này chỉ có hiệu lực trong <strong>15 phút</strong> và chỉ sử dụng được <strong>một lần duy nhất</strong> (BR-AUTH-06).</p>
                <p style='color: #999; font-size: 12px;'>Nếu bạn không yêu cầu hành động này, vui lòng bỏ qua email. Tài khoản của bạn vẫn an toàn.</p>
            </div>";

        await SendEmailAsync(recipientEmail, recipientName, "[EcoLink] Yêu cầu đặt lại mật khẩu tài khoản", htmlBody, ct);
    }

    public async Task SendEmailAsync(string recipientEmail, string recipientName, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = _configuration["MailSettings:Host"] ?? _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
        var portStr = _configuration["MailSettings:Port"] ?? _configuration["EmailSettings:Port"] ?? "587";
        var port = int.TryParse(portStr, out var p) ? p : 587;
        var senderName = _configuration["MailSettings:SenderName"] ?? "EcoLink Support Team";
        var senderEmail = _configuration["MailSettings:SenderEmail"] ?? _configuration["EmailSettings:SenderEmail"] ?? "no-reply@ecolink.vn";
        var username = _configuration["MailSettings:Username"] ?? _configuration["EmailSettings:SenderEmail"] ?? string.Empty;
        var password = _configuration["MailSettings:Password"] ?? _configuration["EmailSettings:SenderPassword"] ?? string.Empty;

        // Nếu ở môi trường dev hoặc chưa cấu hình password thật, ghi log và hoàn tất an toàn
        if (string.IsNullOrWhiteSpace(password) || password.Contains("mock") || password.Contains("xxxx"))
        {
            _logger.LogWarning("[Dev Mock Email] Bỏ qua gửi email thực tế tới {Email}. Tiêu đề: {Subject}", recipientEmail, subject);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(new MailboxAddress(recipientName, recipientEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var cleanPassword = password.Replace(" ", "").Trim();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(username, cleanPassword, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("Gửi email thành công tới {Email}", recipientEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email SMTP tới {Email}", recipientEmail);
            // Tuân thủ BR-NTF-06: Không throw ra ngoài để tránh lộ thông tin nội bộ
        }
    }
}
