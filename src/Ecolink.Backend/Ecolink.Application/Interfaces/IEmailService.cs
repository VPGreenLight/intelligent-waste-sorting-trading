namespace Ecolink.Application.Interfaces;

public interface IEmailService
{
    /// <summary>
    /// Gửi email chứa liên kết đặt lại mật khẩu với token một lần (15 phút)
    /// </summary>
    Task SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetToken, CancellationToken ct = default);

    /// <summary>
    /// Gửi email định dạng HTML chung
    /// </summary>
    Task SendEmailAsync(string recipientEmail, string recipientName, string subject, string htmlBody, CancellationToken ct = default);
}
