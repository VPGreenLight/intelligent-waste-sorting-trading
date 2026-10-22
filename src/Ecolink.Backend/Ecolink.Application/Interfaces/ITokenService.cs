using System.Security.Claims;
using Ecolink.Domain.Entities;

namespace Ecolink.Application.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Tạo Access Token (JWT) cho User
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Sinh Refresh Token ngẫu nhiên (chuỗi an toàn) và TokenHash (SHA-256)
    /// </summary>
    (string RawToken, string TokenHash) GenerateRefreshToken();

    /// <summary>
    /// Băm chuỗi token (SHA-256) để so khớp bảo mật trong CSDL
    /// </summary>
    string HashToken(string token);

    /// <summary>
    /// Đọc Claims từ token kể cả khi token đã hết hạn
    /// </summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

    /// <summary>
    /// Thời điểm hết hạn của Access Token
    /// </summary>
    DateTime GetAccessTokenExpiration();

    /// <summary>
    /// Thời điểm hết hạn của Refresh Token
    /// </summary>
    DateTime GetRefreshTokenExpiration();
}
